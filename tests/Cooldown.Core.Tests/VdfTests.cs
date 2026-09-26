using Cooldown.Steam;

namespace Cooldown.Core.Tests;

public class VdfTests
{
    [Fact]
    public void Parses_library_folders_with_escaped_windows_paths()
    {
        const string text = """
            "libraryfolders"
            {
                "0"
                {
                    "path"    "C:\\Program Files (x86)\\Steam"
                    "apps" { "730" "123" }
                }
                "1" { "path" "D:\\SteamLibrary" }
            }
            """;

        var folders = Vdf.Parse(text).Child("libraryfolders")!;
        Assert.Equal(@"C:\Program Files (x86)\Steam", folders.Child("0")!.Value("path"));
        Assert.Equal(@"D:\SteamLibrary", folders.Child("1")!.Value("path"));
        Assert.Equal("123", folders.Child("0")!.Child("apps")!.Value("730"));
    }

    [Fact]
    public void Keys_are_case_insensitive_and_comments_are_skipped()
    {
        var app = Vdf.Parse("""
            // header comment
            "AppState" { "appid" "1145360" "name" "Hades" "installdir" "Hades" }
            """).Child("appstate")!;

        Assert.Equal("1145360", app.Value("AppID"));
        Assert.Equal("Hades", app.Value("name"));
    }

    [Fact]
    public void Throws_on_unclosed_block()
    {
        Assert.Throws<FormatException>(() => Vdf.Parse("\"a\" { \"b\" \"c\""));
    }
}
