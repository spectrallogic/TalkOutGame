# Neural TTS (Piper) — not in git

Lifelike local voices via [Piper](https://github.com/rhasspy/piper) (MIT).
If these files are missing the game falls back to robotic Windows SAPI voices.

Layout expected:

```
TTS/
  piper/piper.exe        (+ its dlls/espeak-ng-data, from the release zip)
  voices/*.onnx + *.onnx.json
```

## Download

Engine:
```
curl -L -o piper.zip https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip
powershell Expand-Archive piper.zip .
```

Voices (each ~20-65 MB, from rhasspy/piper-voices on Hugging Face — grab both
the .onnx and .onnx.json):

| Character | Voice |
|---|---|
| Officer Glazer | en_US-lessac-medium |
| Chloe | en_US-amy-medium |
| King Aldric IV | en_GB-alan-medium |
| Dennis | en_GB-northern_english_male-medium |
| Benny | en_US-danny-low |

Example:
```
curl -L -o voices/en_US-lessac-medium.onnx https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx
curl -L -o voices/en_US-lessac-medium.onnx.json https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json
```

Voice assignment lives on each character's `NpcSpeaker` component (`piperModel`).
Any Piper voice works — drop it in `voices/` and set the file name.
