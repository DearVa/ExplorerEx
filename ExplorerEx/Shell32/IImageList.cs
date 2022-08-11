using System;
using System.Runtime.InteropServices;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Shell32;

[ComImport, Guid(Shell32Interop.IID_IImageList), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IImageList {
	[PreserveSig]
	int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

	[PreserveSig]
	int ReplaceIcon(int i, IntPtr hicon, ref int pi);

	[PreserveSig]
	int SetOverlayImage(int iImage, int iOverlay);

	[PreserveSig]
	int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

	[PreserveSig]
	int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

	[PreserveSig]
	int Draw(ref ImageListDrawParams pimldp);

	[PreserveSig]
	int Remove(int i);

	[PreserveSig]
	int GetIcon(int i, ILD flags, ref IntPtr picon);

	[PreserveSig]
	int GetImageInfo(int i, ref ImageInfo pImageInfo);

	[PreserveSig]
	int Copy(int iDst, IImageList punkSrc, int iSrc, int uFlags);

	[PreserveSig]
	int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, ref IntPtr ppv);

	[PreserveSig]
	int Clone(ref Guid riid, ref IntPtr ppv);

	[PreserveSig]
	int GetImageRect(int i, ref RectW prc);

	[PreserveSig]
	int GetIconSize(ref int cx, ref int cy);

	[PreserveSig]
	int SetIconSize(int cx, int cy);

	[PreserveSig]
	int GetImageCount(ref int pi);

	[PreserveSig]
	int SetImageCount(int uNewCount);

	[PreserveSig]
	int SetBkColor(int clrBk, ref int pclr);

	[PreserveSig]
	int GetBkColor(ref int pclr);

	[PreserveSig]
	int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

	[PreserveSig]
	int EndDrag();

	[PreserveSig]
	int DragEnter(IntPtr hwndLock, int x, int y);

	[PreserveSig]
	int DragLeave(IntPtr hwndLock);

	[PreserveSig]
	int DragMove(int x, int y);

	[PreserveSig]
	int SetDragCursorImage(ref IImageList punk, int iDrag, int dxHotspot, int dyHotspot);

	[PreserveSig]
	int DragShowNolock(int fShow);

	[PreserveSig]
	int GetDragImage(ref PointW ppt, ref PointW pptHotspot, ref Guid riid, ref IntPtr ppv);

	[PreserveSig]
	int GetItemFlags(int i, ref int dwFlags);

	[PreserveSig]
	int GetOverlayImage(int iOverlay, ref int piIndex);
}

[StructLayout(LayoutKind.Sequential)]
internal struct ImageListStats {
	int cbSize;
	int cAlloc;
	int cUsed;
	int cStandby;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ImageInfo {
	public IntPtr hbmImage;
	public IntPtr hbmMask;
	public int Unused1;
	public int Unused2;
	public RectW rcImage;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageListDrawParams {
	public int cbSize;
	public IntPtr himl;
	public int i;
	public IntPtr hdcDst;
	public int x;
	public int y;
	public int cx;
	public int cy;
	public int xBitmap;        // x offset from the upperLeft of bitmap
	public int yBitmap;        // y offset from the upperLeft of bitmap
	public int rgbBk;
	public int rgbFg;
	public int fStyle;
	public int dwRop;
	public int fState;
	public int Frame;
	public int crEffect;
}

[Flags]
internal enum SHIL : uint {
	ExtraLarge = 0x2,
	Jumbo = 0x4
}

[Flags]
internal enum ILD {
	Transparent = 1
}
