using System;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

namespace TalkOut.Audio
{
    /// Windows text-to-speech through a persistent hidden PowerShell process
    /// running System.Speech (Unity's Mono cannot activate COM in-process).
    /// Offline, ships with Windows, robotic delivery = comedy. Swappable later
    /// for a neural local TTS (Piper/Kokoro) behind this same surface.
    public class SapiVoice
    {
        private Process process;
        private int pendingLines; // >0 while something is queued or speaking

        public bool Available { get; private set; }

        public bool IsSpeaking => Available && System.Threading.Volatile.Read(ref pendingLines) > 0;

        public SapiVoice(string preferredVoiceName, int rate, int pitch)
        {
            try
            {
                Start(preferredVoiceName ?? "", Math.Clamp(rate, -10, 10), Math.Clamp(pitch, -10, 10));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SapiVoice] TTS unavailable: {e.Message}");
                Available = false;
            }
        }

        private void Start(string voiceName, int rate, int pitch)
        {
            int pitchPercent = pitch * 3; // -10..10 -> -30%..+30%
            string script =
                "$ErrorActionPreference='SilentlyContinue';" +
                "Add-Type -AssemblyName System.Speech;" +
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;" +
                $"$s.Rate = {rate};" +
                "foreach($v in $s.GetInstalledVoices()){" +
                $" if($v.VoiceInfo.Name -like '*{voiceName}*' -and '{voiceName}' -ne '')" +
                " { $s.SelectVoice($v.VoiceInfo.Name); break } };" +
                "[Console]::Out.WriteLine('__READY__'); [Console]::Out.Flush();" +
                "while($true){" +
                " $line = [Console]::In.ReadLine();" +
                " if($null -eq $line){ break };" +
                " if($line.Length -gt 0){" +
                "  $ssml = '<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">" +
                $"<prosody pitch=\"{(pitchPercent >= 0 ? "+" : "")}{pitchPercent}%\">' + $line + '</prosody></speak>';" +
                "  try { $s.SpeakSsml($ssml) } catch { $s.Speak(($line -replace '<[^>]+>', ' ')) };" +
                " };" +
                " [Console]::Out.WriteLine('__DONE__'); [Console]::Out.Flush();" +
                "}";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                }
            };
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data == "__DONE__")
                {
                    System.Threading.Interlocked.Decrement(ref pendingLines);
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            Available = true;
        }

        public void Speak(string text)
        {
            if (!Available || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                if (process == null || process.HasExited)
                {
                    Available = false;
                    Debug.LogWarning("[SapiVoice] TTS process died — voice disabled.");
                    return;
                }
                // one line per utterance; XML-escaped for the SSML wrapper
                string line = text.Replace("\r", " ").Replace("\n", " ")
                    .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("'", "&apos;");
                // awkward pauses are load-bearing: turn ellipses and em-dash
                // beats into real dead air
                line = line.Replace("…", "...")
                    .Replace("...", " <break time=\"650ms\"/> ")
                    .Replace(" — ", " <break time=\"350ms\"/> ")
                    .Replace("Mmm.", "Mmm. <break time=\"500ms\"/> ");
                System.Threading.Interlocked.Increment(ref pendingLines);
                process.StandardInput.WriteLine(line);
                process.StandardInput.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SapiVoice] Speak failed: {e.Message}");
                Available = false;
            }
        }

        /// Tears the voice down (used on scene unload).
        public void Stop()
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
                process?.Dispose();
            }
            catch { }
            process = null;
            Available = false;
        }
    }
}
