using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class TesseractOcrTextExtractor : IOcrTextExtractor
{
    private static readonly TimeSpan OcrTimeout = TimeSpan.FromSeconds(45);

    private readonly ILogger<TesseractOcrTextExtractor> logger;

    public TesseractOcrTextExtractor(ILogger<TesseractOcrTextExtractor> logger)
    {
        this.logger = logger;
    }

    public async Task<OcrTextExtractionResult> TryExtractAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        var executablePath = Environment.GetEnvironmentVariable("FILEKITSUNE_TESSERACT_PATH");
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            executablePath = "tesseract";
        }

        var languages = Environment.GetEnvironmentVariable("FILEKITSUNE_OCR_LANGUAGES");
        if (string.IsNullOrWhiteSpace(languages))
        {
            languages = "deu+eng";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add(fullPath);
        process.StartInfo.ArgumentList.Add("stdout");
        process.StartInfo.ArgumentList.Add("-l");
        process.StartInfo.ArgumentList.Add(languages);
        process.StartInfo.ArgumentList.Add("--psm");
        process.StartInfo.ArgumentList.Add("6");
        process.StartInfo.ArgumentList.Add("tsv");

        try
        {
            if (!process.Start())
            {
                return Failed("ocr-unavailable", "Tesseract OCR could not be started.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(OcrTimeout);

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return Failed("ocr-timeout", $"Tesseract OCR exceeded the {OcrTimeout.TotalSeconds:N0} second timeout.");
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(error)
                    ? $"Tesseract OCR exited with code {process.ExitCode}."
                    : TrimMessage(error);
                return Failed("ocr-failed", message);
            }

            var parsed = ParseTsv(output);
            if (string.IsNullOrWhiteSpace(parsed.Text))
            {
                return Failed("ocr-empty", "Tesseract OCR completed but did not return readable text.");
            }

            return new OcrTextExtractionResult
            {
                Succeeded = true,
                Text = parsed.Text,
                Confidence = parsed.Confidence,
                Source = "ocr-tesseract",
                Message = "OCR text extracted with local Tesseract."
            };
        }
        catch (Win32Exception exception)
        {
            logger.LogInformation(exception, "Tesseract OCR executable is unavailable.");
            return Failed(
                "ocr-unavailable",
                "Tesseract OCR is not installed or FILEKITSUNE_TESSERACT_PATH does not point to a usable executable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tesseract OCR failed for {File}", fullPath);
            return Failed("ocr-failed", exception.Message);
        }
    }

    private static OcrTextExtractionResult Failed(string source, string message) =>
        new()
        {
            Succeeded = false,
            Source = source,
            Message = message
        };

    private static (string Text, double? Confidence) ParseTsv(string output)
    {
        var words = new List<string>();
        var confidences = new List<double>();
        var textIndex = -1;
        var confidenceIndex = -1;

        foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = rawLine.Split('\t');
            if (columns.Length == 0)
            {
                continue;
            }

            if (string.Equals(columns[0], "level", StringComparison.OrdinalIgnoreCase))
            {
                textIndex = Array.FindIndex(columns, column => string.Equals(column, "text", StringComparison.OrdinalIgnoreCase));
                confidenceIndex = Array.FindIndex(columns, column => string.Equals(column, "conf", StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (textIndex < 0 || textIndex >= columns.Length)
            {
                continue;
            }

            var text = columns[textIndex].Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            words.Add(text);

            if (confidenceIndex >= 0 &&
                confidenceIndex < columns.Length &&
                double.TryParse(columns[confidenceIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) &&
                confidence >= 0)
            {
                confidences.Add(confidence / 100d);
            }
        }

        if (words.Count == 0)
        {
            return (string.Empty, null);
        }

        var builder = new StringBuilder();
        foreach (var word in words)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(word);
        }

        double? averageConfidence = confidences.Count == 0
            ? null
            : confidences.Average();

        return (builder.ToString(), averageConfidence);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup after a local OCR timeout.
        }
    }

    private static string TrimMessage(string message)
    {
        var normalized = message.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240];
    }
}
