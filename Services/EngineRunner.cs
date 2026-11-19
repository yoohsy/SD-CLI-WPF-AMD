using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CdCliWpf;

public sealed record GenerationRequest(
    string ModelPath,
    string FinalPrompt,
    string NegativePrompt,
    double Width,
    double Height,
    double Steps,
    double Cfg,
    double Seed,
    bool UseLora,
    string LoraPath,
    double LoraWeight,
    string OutputPath);

public sealed record VideoGenerationRequest(
    string DiffusionModelPath,
    string TextEncoderPath,
    string VaePath,
    string InitImagePath,
    string EndImagePath,
    string ClipVisionPath,
    string FinalPrompt,
    string NegativePrompt,
    double Width,
    double Height,
    double Steps,
    double Cfg,
    double Seed,
    int Frames,
    int Fps,
    string OutputPath,
    string PreviewPath);

public sealed record EngineResult(bool Success, string OutputPath, int ExitCode, string Message);

public sealed class EngineRunner
{
    private Process? _process;

    public string EnginePath => Path.Combine(AppContext.BaseDirectory, "engine", "sd-cli.exe");

    public bool EngineExists => File.Exists(EnginePath);

    public void Cancel()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(true);
        }
        catch
        {
            // Процесс мог завершиться между проверкой и вызовом Kill.
        }
    }

    public Task<EngineResult> RunAsync(GenerationRequest request, IProgress<string> log, CancellationToken cancellationToken)
    {
        if (!File.Exists(EnginePath))
            return Task.FromResult(new EngineResult(false, request.OutputPath, -1, "Не найден engine\\sd-cli.exe"));

        var psi = CreateStartInfo();
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add(request.ModelPath);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(request.FinalPrompt.Replace("\"", ""));
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(request.NegativePrompt.Replace("\"", ""));
        psi.ArgumentList.Add("-W");
        psi.ArgumentList.Add(((int)request.Width).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add(((int)request.Height).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--steps");
        psi.ArgumentList.Add(((int)request.Steps).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--cfg-scale");
        psi.ArgumentList.Add(request.Cfg.ToString("0.0", CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--sampling-method");
        psi.ArgumentList.Add("dpm++2m");
        psi.ArgumentList.Add("--vae-tiling");
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add(((int)request.Seed).ToString(CultureInfo.InvariantCulture));

        if (request.UseLora && !string.IsNullOrWhiteSpace(request.LoraPath))
        {
            var loraDir = Path.GetDirectoryName(request.LoraPath);
            if (!string.IsNullOrEmpty(loraDir))
            {
                psi.ArgumentList.Add("--lora-model-dir");
                psi.ArgumentList.Add(loraDir);
            }
        }

        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(request.OutputPath);

        return RunProcessAsync(psi, request.OutputPath, log, cancellationToken);
    }

    public Task<EngineResult> RunVideoAsync(VideoGenerationRequest request, IProgress<string> log, CancellationToken cancellationToken)
    {
        if (!File.Exists(EnginePath))
            return Task.FromResult(new EngineResult(false, request.OutputPath, -1, "Не найден engine\\sd-cli.exe"));

        var psi = CreateStartInfo();
        psi.ArgumentList.Add("-M");
        psi.ArgumentList.Add("vid_gen");
        psi.ArgumentList.Add("--diffusion-model");
        psi.ArgumentList.Add(request.DiffusionModelPath);

        if (!string.IsNullOrWhiteSpace(request.TextEncoderPath))
        {
            psi.ArgumentList.Add("--t5xxl");
            psi.ArgumentList.Add(request.TextEncoderPath);
        }

        if (!string.IsNullOrWhiteSpace(request.VaePath))
        {
            psi.ArgumentList.Add("--vae");
            psi.ArgumentList.Add(request.VaePath);
        }

        if (!string.IsNullOrWhiteSpace(request.ClipVisionPath))
        {
            psi.ArgumentList.Add("--clip_vision");
            psi.ArgumentList.Add(request.ClipVisionPath);
        }

        if (!string.IsNullOrWhiteSpace(request.InitImagePath))
        {
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(request.InitImagePath);
        }

        if (!string.IsNullOrWhiteSpace(request.EndImagePath))
        {
            psi.ArgumentList.Add("--end-img");
            psi.ArgumentList.Add(request.EndImagePath);
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(request.FinalPrompt.Replace("\"", ""));
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(request.NegativePrompt.Replace("\"", ""));
        psi.ArgumentList.Add("-W");
        psi.ArgumentList.Add(((int)request.Width).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add(((int)request.Height).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--steps");
        psi.ArgumentList.Add(((int)request.Steps).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--cfg-scale");
        psi.ArgumentList.Add(request.Cfg.ToString("0.0", CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--sampling-method");
        psi.ArgumentList.Add("dpm++2m");
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add(((int)request.Seed).ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--video-frames");
        psi.ArgumentList.Add(request.Frames.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--fps");
        psi.ArgumentList.Add(request.Fps.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--preview");
        psi.ArgumentList.Add("vae");
        psi.ArgumentList.Add("--preview-path");
        psi.ArgumentList.Add(request.PreviewPath);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(request.OutputPath);

        return RunProcessAsync(psi, request.OutputPath, log, cancellationToken);
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var psi = new ProcessStartInfo
        {
            FileName = EnginePath,
            WorkingDirectory = Path.GetDirectoryName(EnginePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        return psi;
    }

    private async Task<EngineResult> RunProcessAsync(
        ProcessStartInfo psi,
        string outputPath,
        IProgress<string> log,
        CancellationToken cancellationToken)
    {
        var outputFolder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputFolder))
            Directory.CreateDirectory(outputFolder);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process = process;

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                log.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                log.Report("[err] " + e.Data);
        };

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
                // Игнорируем гонку completions/cancel.
            }
        });

        try
        {
            if (!process.Start())
                return new EngineResult(false, outputPath, -1, "Не удалось запустить sd-cli.exe");
        }
        catch (Exception ex)
        {
            _process = null;
            return new EngineResult(false, outputPath, -1, ex.Message);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new EngineResult(false, outputPath, -2, "Генерация отменена");
        }
        finally
        {
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
            process.CancelOutputRead();
            process.CancelErrorRead();
            _process = null;
        }

        var success = process.ExitCode == 0 && File.Exists(outputPath);
        var message = success
            ? "Готово"
            : process.ExitCode == 0
                ? "Файл результата не создан"
                : "sd-cli завершился с кодом " + process.ExitCode.ToString(CultureInfo.InvariantCulture);

        return new EngineResult(success, outputPath, process.ExitCode, message);
    }
}
