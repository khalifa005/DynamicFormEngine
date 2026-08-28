using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;

/// <summary>
/// Registers the Noto fonts shipped with the PDF exporter. Local Windows has Arial with Arabic
/// glyphs; most deployment hosts do not, so QuestPDF falls back to Lato and prints tofu boxes.
/// Embedded fonts keep English and Arabic identical in every environment.
/// </summary>
internal static class SurveyPdfFonts
{
    public const string LatinFamily = "Noto Sans";
    public const string ArabicFamily = "Noto Naskh Arabic";

    private const string ResourcePrefix = "KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf.Resources.";

    private static readonly string[] FontResourceNames =
    [
        ResourcePrefix + "NotoSans-Regular.ttf",
        ResourcePrefix + "NotoSans-SemiBold.ttf",
        ResourcePrefix + "NotoNaskhArabic-Regular.ttf",
        ResourcePrefix + "NotoNaskhArabic-SemiBold.ttf"
    ];

    private static readonly object Sync = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Sync)
        {
            if (_registered)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = false;

            foreach (var resourceName in FontResourceNames)
            {
                FontManager.RegisterFontFromEmbeddedResource(resourceName);
            }

            _registered = true;
        }
    }
}
