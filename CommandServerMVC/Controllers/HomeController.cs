using System.Diagnostics;
using System.Text;
using CommandServerMVC.Models;

using Microsoft.AspNetCore.Mvc;

namespace CommandServerMVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _Logger;

    public HomeController(ILogger<HomeController> Logger) => _Logger = Logger;

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Execute(CommandInfo Command)
    {
        try
        {
            _Logger.LogInformation("Выполнение команды {0}", Command);

            var process_info = new ProcessStartInfo(Command.Name)
            {
                UseShellExecute        = Command.UseShellExecute,
                Arguments              = Command.Args,
                CreateNoWindow         = true,
                RedirectStandardOutput = !Command.UseShellExecute,
            };

            var process = Process.Start(process_info);
            if (process is null)
                return BadRequest("Не удалось запустить процесс");

            process.EnableRaisingEvents = true;
            if(!Command.UseShellExecute)
                process.BeginOutputReadLine();

            var out_text = new StringBuilder();

            process.OutputDataReceived += (_, e) => out_text.AppendLine(e.Data);


            var process_completed = new TaskCompletionSource();
            process.Exited += (_, _) => process_completed.SetResult();

            await process_completed.Task;

            _Logger.LogInformation("Выполнение команды {0} выполнено успешно", Command);

            Directory.CreateDirectory("logs");
            await System.IO.File.AppendAllTextAsync("logs/commands.log", $"time:{DateTime.Now}\r\n");
            await System.IO.File.AppendAllTextAsync("logs/commands.log", $"command:{Command}\r\n");
            await System.IO.File.AppendAllTextAsync("logs/commands.log", $"out:{out_text}\r\n");
            await System.IO.File.AppendAllTextAsync("logs/commands.log", "---------------------\r\n");


            return View(new CommandExecuteResult
            {
                Command = Command,
                ExitCode = process.ExitCode,
                Output = out_text.ToString(),
            });
        }
        catch (Exception e)
        {
            _Logger.LogError(e, "Ошибка при выполнении команды {0}", Command);
            return BadRequest(e.Message);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
