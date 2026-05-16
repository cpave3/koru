using Spectre.Console;
using Spectre.Console.Rendering;

namespace Koru.Cli.Core.Util;

internal sealed class VimAnsiConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;

    public VimAnsiConsole(IAnsiConsole inner)
    {
        _inner = inner;
        Input = new VimInput(inner.Input);
    }

    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input { get; }
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;
    public void Clear(bool home) => _inner.Clear(home);
    public void Write(IRenderable renderable) => _inner.Write(renderable);

    private sealed class VimInput : IAnsiConsoleInput
    {
        private readonly IAnsiConsoleInput _inner;
        public VimInput(IAnsiConsoleInput inner) => _inner = inner;
        public bool IsKeyAvailable() => _inner.IsKeyAvailable();
        public ConsoleKeyInfo? ReadKey(bool intercept) => Translate(_inner.ReadKey(intercept));
        public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken ct)
            => Translate(await _inner.ReadKeyAsync(intercept, ct));

        private static ConsoleKeyInfo? Translate(ConsoleKeyInfo? key)
        {
            if (key is null) return null;
            var k = key.Value;
            if (k.Modifiers != 0) return key;
            return k.KeyChar switch
            {
                'j' => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
                'k' => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false),
                'h' => new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false),
                'l' => new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false),
                'g' => new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false),
                'G' => new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false),
                _ => key,
            };
        }
    }
}
