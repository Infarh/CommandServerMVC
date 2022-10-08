using System.Diagnostics;

using CommandServerMVC.Models;

using Microsoft.AspNetCore.Mvc;

namespace CommandServerMVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _Logger;

    public HomeController(ILogger<HomeController> Logger) => _Logger = Logger;

    public IActionResult Index() => View();

    [HttpPost]
    public IActionResult Execute(CommandInfo Command)
    {
        try
        {
            _Logger.LogInformation("Выполнение команды {0}", Command);



            _Logger.LogInformation("Выполнение команды {0} выполнено успешно", Command);
            return RedirectToAction("Index");
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
