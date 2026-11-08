using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WingmanInstaller
{
    // Use the Explorer-style common dialog instead of the legacy folder tree.
    internal static class FolderPicker
    {
        private const uint PickFolders = 0x20;
        private const uint ForceFileSystem = 0x40;
        private const uint PathMustExist = 0x800;
        private const uint NoChangeDirectory = 0x8;
        private const uint FileSystemPath = 0x80058000;
        private const int Cancelled = unchecked((int)0x800704C7);

        public static string Show(IWin32Window owner, string currentFolder)
        {
            object instance = new FileOpenDialog();
            IFileDialog dialog = (IFileDialog)instance;
            IShellItem selected = null;
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | PickFolders | ForceFileSystem | PathMustExist | NoChangeDirectory);
                dialog.SetTitle("Selecione a pasta do Project Wingman");
                dialog.SetOkButtonLabel("Selecionar pasta");
                dialog.SetFileNameLabel("Pasta:");
                Guid client = new Guid("10F916D6-5245-4B75-90A5-7F4FF54B1EBA");
                dialog.SetClientGuid(ref client);

                string initial = (currentFolder ?? "").Trim().Trim('"');
                if (Directory.Exists(initial))
                {
                    IShellItem folder = CreateItem(Path.GetFullPath(initial));
                    try { dialog.SetFolder(folder); }
                    finally { Marshal.FinalReleaseComObject(folder); }
                }
                else
                {
                    string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (Directory.Exists(documents))
                    {
                        IShellItem folder = CreateItem(documents);
                        try { dialog.SetDefaultFolder(folder); }
                        finally { Marshal.FinalReleaseComObject(folder); }
                    }
                }

                int result = dialog.Show(owner.Handle);
                if (result == Cancelled) return null;
                Marshal.ThrowExceptionForHR(result);
                dialog.GetResult(out selected);
                IntPtr path = IntPtr.Zero;
                try
                {
                    selected.GetDisplayName(FileSystemPath, out path);
                    return Marshal.PtrToStringUni(path);
                }
                finally { if (path != IntPtr.Zero) Marshal.FreeCoTaskMem(path); }
            }
            finally
            {
                if (selected != null) Marshal.FinalReleaseComObject(selected);
                Marshal.FinalReleaseComObject(instance);
            }
        }

        private static IShellItem CreateItem(string path)
        {
            Guid itemId = typeof(IShellItem).GUID;
            IShellItem item;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item));
            return item;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext,
            ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        // IModalWindow::Show is the first slot. Keep the native IFileDialog
        // method order; unused slots are included for correct COM dispatch.
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, uint alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint nameType, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
