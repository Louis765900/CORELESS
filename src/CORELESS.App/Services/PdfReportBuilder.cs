using Coreless.Models;
using Coreless.Services.Benchmarks;
using Coreless.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Coreless.Services;

/// <summary>Renders a graphical PDF analysis report via QuestPDF.</summary>
public static class PdfReportBuilder
{
    private const string Ink = "#141A24";
    private const string Muted = "#8B98A9";
    private const string Line = "#E0E4EA";
    private const string Dark = "#0B0E14";
    private const string Accent = "#35C9F0";
    private const string Red = "#E4002B";
    private const string Track = "#E4E7EC";

    public static void Build(string path, string machine, string os, IEnumerable<InfoItem> summary,
                             IEnumerable<ComponentViewModel> components, IEnumerable<BenchmarkOutcome> benchmarks)
    {
        var comps = components.ToList();
        var benches = benchmarks.ToList();
        var stats = summary.ToList();

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink));

                page.Header().Element(h => Header(h, machine, os));
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);
                    BenchmarkOutcome? index = benches.FirstOrDefault(b => b.Category == BenchCategory.Composite);
                    if (index is not null) IndexHero(col, index);
                    Summary(col, stats);
                    VisualOverview(col, comps);
                    if (benches.Count > 0) Benchmarks(col, benches);
                    SectionTitle(col, "Composants & capteurs");
                    foreach (ComponentViewModel c in comps) Component(col, c);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("CORELESS — analyse système   •   page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        }).GeneratePdf(path);
    }

    private static void Header(IContainer c, string machine, string os)
    {
        c.Background(Dark).Padding(16).Row(row =>
        {
            row.ConstantItem(30).Height(30).Background(Accent);
            row.RelativeItem().PaddingLeft(12).Column(col =>
            {
                col.Item().Text("CORELESS").FontSize(20).Bold().FontColor("#FFFFFF");
                col.Item().Text("Rapport d'analyse système").FontSize(9).FontColor(Muted);
            });
            row.ConstantItem(220).Column(col =>
            {
                col.Item().AlignRight().Text(machine).FontSize(11).Bold().FontColor("#E6EDF3");
                col.Item().AlignRight().Text(os).FontSize(8).FontColor(Muted);
                col.Item().AlignRight().Text($"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}").FontSize(8).FontColor(Muted);
            });
        });
    }

    private static void SectionTitle(ColumnDescriptor col, string text)
        => col.Item().PaddingTop(4).Text(text).FontSize(13).Bold().FontColor(Ink);

    private static void IndexHero(ColumnDescriptor col, BenchmarkOutcome index)
    {
        col.Item().Background(Dark).Padding(18).Row(row =>
        {
            row.RelativeItem().Column(cc =>
            {
                cc.Item().Text("INDICE CORELESS").FontSize(11).Bold().FontColor(Muted);
                cc.Item().Text("Score global pondéré, tous modules exécutés").FontSize(8).FontColor(Muted);
            });
            row.ConstantItem(140).AlignRight().Column(cc =>
            {
                cc.Item().AlignRight().Text(index.ScoreValue).FontSize(30).Bold().FontColor(Accent);
                cc.Item().AlignRight().Text(index.ScoreUnit).FontSize(9).FontColor(Muted);
            });
        });
    }

    /// <summary>Simple proportional two-segment bar (filled + track) — QuestPDF has no chart primitive,
    /// so gauges are faked with relative-width containers rather than a real drawing surface.</summary>
    private static void Bar(IContainer container, float fraction, string color)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        container.Height(7).Row(row =>
        {
            row.RelativeItem(fraction).Background(color);
            row.RelativeItem(1 - fraction).Background(Track);
        });
    }

    private static void VisualOverview(ColumnDescriptor col, List<ComponentViewModel> comps)
    {
        var withGauges = comps.Where(c => c.HasTemp || c.HasLoad).ToList();
        if (withGauges.Count == 0) return;

        SectionTitle(col, "Aperçu visuel");
        foreach (ComponentViewModel c in withGauges)
        {
            string hex = Hex6(c.ColorHex);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.ConstantItem(120).Text(c.Name).FontSize(8.5f).SemiBold().FontColor(Ink);

                row.ConstantItem(140).PaddingRight(10).Column(cc =>
                {
                    if (c.HasTemp && c.TempHeadline?.Raw is float t && !float.IsNaN(t))
                    {
                        cc.Item().Text($"{t:0.0} °C").FontSize(7).FontColor(Muted);
                        cc.Item().Element(e => Bar(e, t / 100f, t >= 80 ? Red : hex));
                    }
                });

                row.RelativeItem().Column(cc =>
                {
                    if (c.HasLoad && c.LoadHeadline?.Raw is float l && !float.IsNaN(l))
                    {
                        cc.Item().Text($"{l:0} % charge").FontSize(7).FontColor(Muted);
                        cc.Item().Element(e => Bar(e, l / 100f, Accent));
                    }
                });
            });
        }
    }

    private static void Summary(ColumnDescriptor col, List<InfoItem> summary)
    {
        SectionTitle(col, "Résumé système");
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            foreach (InfoItem i in summary)
            {
                table.Cell().Element(Cell).Text(i.Label).FontColor(Muted);
                table.Cell().Element(Cell).Text(i.Value).SemiBold();
            }
        });
    }

    private static void Benchmarks(ColumnDescriptor col, List<BenchmarkOutcome> benches)
    {
        List<BenchmarkOutcome> individual = benches.Where(b => b.Category != BenchCategory.Composite).ToList();
        if (individual.Count == 0) return;

        SectionTitle(col, "Benchmarks");
        foreach (BenchmarkOutcome b in individual)
        {
            col.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text(b.Title).FontSize(11).Bold().FontColor(Ink);
                row.ConstantItem(200).AlignRight().Text($"{b.ScoreLabel}: {b.ScoreValue} {b.ScoreUnit}")
                    .FontSize(10).Bold().FontColor(Accent);
            });
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                foreach (InfoItem d in b.Details)
                {
                    table.Cell().Element(Cell).Text(d.Label).FontColor(Muted);
                    table.Cell().Element(Cell).Text(d.Value).SemiBold();
                }
            });
        }
    }

    private static void Component(ColumnDescriptor col, ComponentViewModel comp)
    {
        string hex = Hex6(comp.ColorHex);
        col.Item().PaddingTop(8).Row(row =>
        {
            row.ConstantItem(34).Height(22).Background(hex).AlignCenter().AlignMiddle()
                .Text(comp.ShortCode).FontSize(8).Bold().FontColor(Dark);
            row.RelativeItem().PaddingLeft(8).Column(cc =>
            {
                cc.Item().Text(comp.Name).FontSize(12).Bold().FontColor(Ink);
                cc.Item().Text(comp.Category).FontSize(8).FontColor(Muted);
            });
        });

        foreach (SensorGroupViewModel g in comp.Groups)
        {
            col.Item().PaddingTop(4).Text(g.Title).FontSize(9).Bold().FontColor(hex);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(3); });
                foreach (SensorViewModel s in g.Sensors)
                {
                    table.Cell().Element(Cell).Text(s.Name);
                    table.Cell().Element(Cell).Text($"{s.Value} {s.Unit}").SemiBold();
                    table.Cell().Element(Cell).Text($"min {s.Min}  /  max {s.Max}").FontColor(Muted);
                }
            });
        }

        foreach (ComponentViewModel sub in comp.SubComponents)
            Component(col, sub);
    }

    private static IContainer Cell(IContainer c)
        => c.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(3).PaddingHorizontal(6);

    private static string Hex6(string argb)
    {
        string h = argb.TrimStart('#');
        if (h.Length == 8) h = h.Substring(2); // drop alpha
        return "#" + h;
    }
}
