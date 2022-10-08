using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandServerMVC.Models;

public class CommandExecuteResult
{
    public CommandInfo Command { get; init; }

    public int ExitCode { get; init; }

    public string Output { get; init; }
}
