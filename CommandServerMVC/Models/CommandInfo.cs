using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CommandServerMVC.Models;

public class CommandInfo
{
    [Required]
    [Display(Name = "Команда")]
    public string Name { get; init; }

    [Display(Name = "Аргументы")]
    public string? Args { get; init; }

    [DefaultValue(false)]
    [Display(Name = "Использовать shell?")]
    public bool UseShellExecute { get; init; }

    public override string ToString() => $"{Name} {Args} Use shell:{UseShellExecute}";
}
