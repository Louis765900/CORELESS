using System.Text;
using Coreless.Models;
using Coreless.ViewModels;

namespace Coreless.Services;

/// <summary>Turns the live component tree into a plain-text analysis report.</summary>
public static class ReportBuilder
{
    public static string BuildText(string machine, string os, IEnumerable<InfoItem> summary,
                                   IEnumerable<ComponentViewModel> components)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================================");
        sb.AppendLine("   CORELESS — RAPPORT D'ANALYSE SYSTÈME");
        sb.AppendLine("========================================================");
        sb.AppendLine($"Machine   : {machine}");
        sb.AppendLine($"Système   : {os}");
        sb.AppendLine($"Généré le : {DateTime.Now:dddd d MMMM yyyy — HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("--- RÉSUMÉ ---------------------------------------------");
        foreach (InfoItem i in summary)
            sb.AppendLine($"{i.Label,-18}: {i.Value}");
        sb.AppendLine();

        foreach (ComponentViewModel c in components)
            AppendComponent(sb, c, 0);

        sb.AppendLine("========================================================");
        sb.AppendLine("   Fin du rapport — CORELESS");
        sb.AppendLine("========================================================");
        return sb.ToString();
    }

    private static void AppendComponent(StringBuilder sb, ComponentViewModel c, int depth)
    {
        string indent = new string(' ', depth * 2);
        if (depth == 0)
        {
            sb.AppendLine($"=== [{c.ShortCode}] {c.Category.ToUpperInvariant()} — {c.Name} ===");
        }
        else
        {
            sb.AppendLine($"{indent}· {c.Name}");
        }

        foreach (SensorGroupViewModel g in c.Groups)
        {
            sb.AppendLine($"{indent}  [{g.Title}]");
            foreach (SensorViewModel s in g.Sensors)
                sb.AppendLine($"{indent}    {s.Name,-28} {s.Value,10} {s.Unit,-4}  (min {s.Min}  max {s.Max})");
        }

        foreach (ComponentViewModel sub in c.SubComponents)
            AppendComponent(sb, sub, depth + 1);

        sb.AppendLine();
    }
}
