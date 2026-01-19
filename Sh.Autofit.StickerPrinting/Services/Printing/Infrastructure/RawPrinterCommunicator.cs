using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing.Printing;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Infrastructure;

/// <summary>
/// Sends raw data to Windows printer using Win32 API
/// Based on Microsoft's RawPrinterHelper pattern
/// </summary>
public class RawPrinterCommunicator : IRawPrinterCommunicator
{
    #region Win32 API Declarations

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    #endregion

    public async Task<bool> SendStringToPrinterAsync(string printerName, string data)
    {
        return await Task.Run(() =>
        {
            IntPtr hPrinter = IntPtr.Zero;
            bool success = false;

            try
            {
                // Open printer
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open printer '{printerName}'");
                }

                // Start document
                var docInfo = new DOCINFOA
                {
                    pDocName = "Label Print Job",
                    pOutputFile = null,
                    pDataType = "RAW" // Critical: RAW data type bypasses driver processing
                };

                if (!StartDocPrinter(hPrinter, 1, docInfo))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start document");
                }

                try
                {
                    // Start page
                    if (!StartPagePrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start page");
                    }

                    // Write data
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);

                    try
                    {
                        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

                        if (!WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int bytesWritten))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write to printer");
                        }

                        success = (bytesWritten == bytes.Length);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }

                    // End page
                    if (!EndPagePrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to end page");
                    }
                }
                finally
                {
                    // End document
                    if (!EndDocPrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to end document");
                    }
                }

                return success;
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                {
                    ClosePrinter(hPrinter);
                }
            }
        });
    }

    public async Task<bool> SendBytesToPrinterAsync(string printerName, byte[] data)
    {
        return await Task.Run(() =>
        {
            IntPtr hPrinter = IntPtr.Zero;
            bool success = false;

            try
            {
                // Open printer
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open printer '{printerName}'");
                }

                // Start document
                var docInfo = new DOCINFOA
                {
                    pDocName = "Label Print Job",
                    pOutputFile = null,
                    pDataType = "RAW"
                };

                if (!StartDocPrinter(hPrinter, 1, docInfo))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start document");
                }

                try
                {
                    // Start page
                    if (!StartPagePrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start page");
                    }

                    // Write data
                    IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);

                    try
                    {
                        Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);

                        if (!WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out int bytesWritten))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write to printer");
                        }

                        success = (bytesWritten == data.Length);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }

                    // End page
                    if (!EndPagePrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to end page");
                    }
                }
                finally
                {
                    // End document
                    if (!EndDocPrinter(hPrinter))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to end document");
                    }
                }

                return success;
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                {
                    ClosePrinter(hPrinter);
                }
            }
        });
    }

    public async Task<bool> IsPrinterAvailableAsync(string printerName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var printerSettings = new PrinterSettings { PrinterName = printerName };
                return printerSettings.IsValid;
            }
            catch
            {
                return false;
            }
        });
    }
}
