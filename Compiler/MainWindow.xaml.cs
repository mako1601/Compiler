using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Storage.Pickers;

using WinUIEx;

using static Compiler.Parser;
using static Compiler.Assembly;

namespace Compiler
{
    public sealed partial class MainWindow : WindowEx
    {
        public string FilePath { get; private set; }
        private bool IsSaved { get; set; }
        private bool IsClosing { get; set; }
        private List<Token> Tokens { get; set; }
        private string Assembly { get; set; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.PersistenceId = "MainWindow";
            this.ExtendsContentIntoTitleBar = true;
            this.SystemBackdrop = new MicaBackdrop();

            FilePath = string.Empty;
            IsSaved = true;
            IsClosing = false;
            Tokens = [];
            InitializeParser();
            Assembly = string.Empty;
        }

        private void TextEditor_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            IsSaved = false;
            var lexer = new Lexer(TextEditor.Editor.GetText(long.MaxValue));
            List<Token> _tokens = lexer.Tokenize();

            if (!Tokens.Equals(_tokens))
            {
                Tokens = _tokens;
                LexerOutput.ItemsSource = Tokens;
            }

            MainOutput.Text = string.Empty;
            if (lexer.Errors.Count > 0)
            {
                foreach (var error in lexer.Errors)
                {
                    MainOutput.Text += error + "\n";
                }
            }
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".txt");
            var file = await openPicker.PickSingleFileAsync();

            if (file is not null)
            {
                IsSaved = true;
                FilePath = file.Path;
                using var reader = new StreamReader(FilePath);
                TextEditor.Editor.SetText(await reader.ReadToEndAsync());
                TextEditor_KeyUp(sender, null);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (FilePath.Equals(string.Empty))
            {
                await Task.Run(() => SaveAs_Click(sender, e));
            }
            else
            {
                IsSaved = true;
                using var writer = new StreamWriter(FilePath);
                await writer.WriteAsync(TextEditor.Editor.GetText(long.MaxValue));
            }
        }

        private async void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text documents", [".txt"]);
            var file = await savePicker.PickSaveFileAsync();

            if (file is not null)
            {
                IsSaved = true;
                FilePath = file.Path;
                using var writer = new StreamWriter(FilePath);
                await writer.WriteAsync(TextEditor.Editor.GetText(long.MaxValue));
            }
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            if (IsSaved is false)
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Save your work?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Don't Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();

                switch (result)
                {
                    case ContentDialogResult.Primary: SaveFile(); ComponentReset(); break;
                    case ContentDialogResult.Secondary: ComponentReset(); break;
                    case ContentDialogResult.None: break;
                }
            }
            else
            {
                ComponentReset();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            if (IsClosing is false && IsSaved is false)
            {
                IsClosing = true;
                args.Handled = true;

                ContentDialog dialog = new()
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Save your work?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Don't Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();

                switch (result)
                {
                    case ContentDialogResult.Primary: SaveFile(); Application.Current.Exit(); break;
                    case ContentDialogResult.Secondary: Application.Current.Exit(); break;
                    case ContentDialogResult.None: IsClosing = false; break;
                }
            }
        }

        private void Compile_Click(object sender, RoutedEventArgs e)
        {
            if (Tokens.Count > 0)
            {
                if (FilePath.Equals(string.Empty) || IsSaved is false)
                {
                    try
                    {
                        SaveFile();
                    }
                    catch (FileNotFoundException)
                    {
                        return;
                    }
                }

                MainOutput.Text = string.Empty;

                var lexer = new Lexer(TextEditor.Editor.GetText(long.MaxValue));
                lexer.Tokenize();

                if (lexer.Errors.Count > 0)
                {
                    foreach (var error in lexer.Errors)
                    {
                        MainOutput.Text += error + "\n";
                    }
                    return;
                }

                try
                {
                    ShiftConvolution(Tokens);
                }
                catch (SyntaxException ex)
                {
                    MainOutput.Text += ex.Message + "\n";
                    OutputsReset();
                    return;
                }

                var semantic = new Semantic(Tokens);
                semantic.CheckDeclaration();

                if (semantic.Errors.Count > 0)
                {
                    foreach (var error in semantic.Errors)
                    {
                        MainOutput.Text += error + "\n";
                    }
                    OutputsReset();
                    return;
                }

                var rpn = new RPN(Tokens);
                RPNOutput.Text = string.Empty;

                foreach (var token in rpn.PostfixNotation)
                {
                    if (token.Type is TokenType.LabelInfo)
                    {
                        if (token.Value.Equals("true"))
                        {
                            RPNOutput.Text += $"Conditional jump on lie ({rpn.PostfixNotation[token.Line].Value})\n";
                        }
                        else if (token.Value.Equals("false"))
                        {
                            RPNOutput.Text += $"Conditional jump on truth ({rpn.PostfixNotation[token.Line].Value})\n";
                        }
                        else
                        {
                            RPNOutput.Text += $"Unconditional jump ({rpn.PostfixNotation[token.Line].Value})\n";
                        }
                    }
                    else if (token.Type is TokenType.Label)
                    {
                        RPNOutput.Text += token.Value + ":\n";
                    }
                    else
                    {
                        RPNOutput.Text += token.Value + "\n";
                    }
                }

                Assembly = GetAssemblyCode(rpn, semantic);
                AssemblyOutput.Text = Assembly;

                string filePath = Path.ChangeExtension(FilePath, null);
                using var writer = new StreamWriter($"{filePath}.asm");
                writer.Write(AssemblyOutput.Text);
                writer.Close();

                var cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.Arguments = $"/C {AppDomain.CurrentDomain.BaseDirectory}nasm.exe -fwin64 {filePath}.asm"
                    + $"&& {AppDomain.CurrentDomain.BaseDirectory}gcc/bin/gcc.exe {filePath}.obj -o {filePath}.exe";
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();

                MainOutput.Text = "Compilation completed successfully!\n";
            }
            else
            {
                OutputsReset();
            }
        }

        private void SaveFile()
        {
            if (FilePath.Equals(string.Empty))
            {
                var savePicker = new FileSavePicker();
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("Text documents", [".txt"]);
                var file = savePicker.PickSaveFileAsync().GetAwaiter().GetResult();

                if (file is not null)
                {
                    IsSaved = true;
                    FilePath = file.Path;
                    using var writer = new StreamWriter(FilePath);
                    writer.Write(TextEditor.Editor.GetText(long.MaxValue));
                    writer.Close();
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            else
            {
                IsSaved = true;
                using var writer = new StreamWriter(FilePath);
                writer.Write(TextEditor.Editor.GetText(long.MaxValue));
                writer.Close();
            }
        }

        private void ComponentReset()
        {
            Tokens.Clear();
            FilePath = string.Empty;
            IsSaved  = true;
            Assembly = string.Empty;

            LexerOutput.ItemsSource = null;
            RPNOutput.Text          = string.Empty;
            MainOutput.Text         = string.Empty;
            AssemblyOutput.Text     = string.Empty;
            TextEditor.Editor.SetText(string.Empty);
        }

        private void OutputsReset()
        {
            Assembly            = string.Empty;
            RPNOutput.Text      = string.Empty;
            AssemblyOutput.Text = string.Empty;
        }
    }
}
