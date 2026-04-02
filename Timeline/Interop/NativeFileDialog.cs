using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Native Windows save/open file dialog via comdlg32. Works in Unity/BepInEx runtime.
    /// </summary>
    public static class NativeFileDialog
    {
        private const int MaxPath = 260;

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetSaveFileNameW(ref OpenFileNameW ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileNameW(ref OpenFileNameW ofn);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
        private struct OpenFileNameW
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
        }

        private const int OfnPathmustexist = 0x00000800;
        private const int OfnOverwriteprompt = 0x00000002;
        private const int OfnFilemustexist = 0x00001000;

        private static readonly string DefaultTimelineFolder = @"D:\Honey Select\UserData\Timeline";

        private static string GetInitialDir()
        {
            try
            {
                if (!Directory.Exists(DefaultTimelineFolder))
                    Directory.CreateDirectory(DefaultTimelineFolder);
                return DefaultTimelineFolder;
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>
        /// Shows a save file dialog. Returns the chosen path or null if cancelled.
        /// Restores the process current directory after the dialog closes (Windows changes it when navigating).
        /// </summary>
        public static string? SaveFile(string title, string defaultExt, string filter)
        {
            string? cwd = null;
            try
            {
                cwd = Directory.GetCurrentDirectory();
            }
            catch { /* ignore */ }
            int bufferSize = (MaxPath + 1) * 2;
            IntPtr filePtr = Marshal.AllocCoTaskMem(bufferSize);
            try
            {
                var initBytes = new byte[bufferSize];
                Marshal.Copy(initBytes, 0, filePtr, bufferSize);
                var ofn = new OpenFileNameW
                {
                    lStructSize = Marshal.SizeOf<OpenFileNameW>(),
                    hwndOwner = IntPtr.Zero,
                    lpstrFilter = filter ?? "All files (*.*)\0*.*\0",
                    nFilterIndex = 1,
                    lpstrFile = filePtr,
                    nMaxFile = MaxPath + 1,
                    lpstrInitialDir = GetInitialDir(),
                    lpstrTitle = title ?? "Save",
                    Flags = OfnOverwriteprompt | OfnPathmustexist,
                    lpstrDefExt = defaultExt ?? ""
                };
                if (!GetSaveFileNameW(ref ofn))
                    return null;
                var bytes = new byte[bufferSize];
                Marshal.Copy(filePtr, bytes, 0, bytes.Length);
                int len = IndexOfNullTerminator(bytes);
                return Encoding.Unicode.GetString(bytes, 0, len).TrimEnd('\0');
            }
            finally
            {
                if (filePtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePtr);
                if (!string.IsNullOrEmpty(cwd))
                {
                    try { Directory.SetCurrentDirectory(cwd); }
                    catch { /* ignore */ }
                }
            }
        }

        private static int IndexOfNullTerminator(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                if (bytes[i] == 0 && bytes[i + 1] == 0)
                    return i;
            }
            return bytes.Length;
        }

        /// <summary>
        /// Shows an open file dialog. Returns the chosen path or null if cancelled.
        /// Restores the process current directory after the dialog closes (Windows changes it when navigating).
        /// </summary>
        public static string? OpenFile(string title, string filter)
        {
            string? cwd = null;
            try
            {
                cwd = Directory.GetCurrentDirectory();
            }
            catch { /* ignore */ }
            int bufferSize = (MaxPath + 1) * 2;
            IntPtr filePtr = Marshal.AllocCoTaskMem(bufferSize);
            try
            {
                var initBytes = new byte[bufferSize];
                Marshal.Copy(initBytes, 0, filePtr, bufferSize);
                var ofn = new OpenFileNameW
                {
                    lStructSize = Marshal.SizeOf<OpenFileNameW>(),
                    hwndOwner = IntPtr.Zero,
                    lpstrFilter = filter ?? "All files (*.*)\0*.*\0",
                    nFilterIndex = 1,
                    lpstrFile = filePtr,
                    nMaxFile = MaxPath + 1,
                    lpstrInitialDir = GetInitialDir(),
                    lpstrTitle = title ?? "Open",
                    Flags = OfnFilemustexist | OfnPathmustexist,
                    lpstrDefExt = ""
                };
                if (!GetOpenFileNameW(ref ofn))
                    return null;
                var bytes = new byte[bufferSize];
                Marshal.Copy(filePtr, bytes, 0, bytes.Length);
                int len = IndexOfNullTerminator(bytes);
                return Encoding.Unicode.GetString(bytes, 0, len).TrimEnd('\0');
            }
            finally
            {
                if (filePtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePtr);
                if (!string.IsNullOrEmpty(cwd))
                {
                    try { Directory.SetCurrentDirectory(cwd); }
                    catch { /* ignore */ }
                }
            }
        }
    }
}
