# Models (not in git)

## Speech-to-text: Whisper base.en (~148 MB)

```
curl -L -o ggml-base.en.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
```

# LLM Models

GGUF model files live here in dev and are bundled into builds from here.
They are gitignored — download manually:

## Default model: Dolphin 3.0 Llama 3.1 8B Q4_K_M (~4.9 GB)

Uncensored community fine-tune with strong instruction-following.
License: Llama 3.1 Community License — commercial use OK, ship "Built with Llama" attribution.

```
curl -L -o Dolphin3.0-Llama3.1-8B-Q4_K_M.gguf https://huggingface.co/bartowski/Dolphin3.0-Llama3.1-8B-GGUF/resolve/main/Dolphin3.0-Llama3.1-8B-Q4_K_M.gguf
```

## Smaller/older fallback: Phi-3.5-mini-instruct Q4_K_M (~2.3 GB, MIT license)

```
curl -L -o Phi-3.5-mini-instruct-Q4_K_M.gguf https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q4_K_M.gguf
```

## Low-spec option: Qwen2.5-1.5B-Instruct Q4_K_M (~1 GB, Apache 2.0)

```
curl -L -o Qwen2.5-1.5B-Instruct-Q4_K_M.gguf https://huggingface.co/bartowski/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/Qwen2.5-1.5B-Instruct-Q4_K_M.gguf
```

The active model file name is set on the `LlmConfig` asset (`Assets/GameData/LlmConfig.asset`).
Any GGUF chat model can be swapped in by dropping it here and updating that config —
check the model's license before shipping it in a commercial build.
