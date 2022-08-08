using System;
using System.Runtime.InteropServices;
using ExplorerEx.Win32;

namespace ExplorerEx.Shell32; 

[Flags]
public enum ThumbnailOptions {  // IShellItemImageFactory Flags: https://msdn.microsoft.com/en-us/library/windows/desktop/bb761082%28v=vs.85%29.aspx
	None = 0x00,                // Shrink the bitmap as necessary to fit, preserving its aspect ratio. Returns thumbnail if available, else icon.
	BiggerSizeOk = 0x01,        // Passed by callers if they want to stretch the returned image themselves. For example, if the caller passes an icon size of 80x80, a 96x96 thumbnail could be returned. This action can be used as a performance optimization if the caller expects that they will need to stretch the image. Note that the Shell implementation of IShellItemImageFactory performs a GDI stretch blit. If the caller wants a higher quality image stretch than provided through that mechanism, they should pass this flag and perform the stretch themselves.
	InMemoryOnly = 0x02,        // Return the item only if it is already in memory. Do not access the disk even if the item is cached. Note that this only returns an already-cached icon and can fall back to a per-class icon if an item has a per-instance icon that has not been cached. Retrieving a thumbnail, even if it is cached, always requires the disk to be accessed, so GetImage should not be called from the UI thread without passing SIIGBF_MEMORYONLY.
	IconOnly = 0x04,            // Return only the icon, never the thumbnail.
	ThumbnailOnly = 0x08,       // Return only the thumbnail, never the icon. Note that not all items have thumbnails, so SIIGBF_THUMBNAILONLY will cause the method to fail in these cases.
	InCacheOnly = 0x10,         // Allows access to the disk, but only to retrieve a cached item. This returns a cached thumbnail if it is available. If no cached thumbnail is available, it returns a cached per-instance icon but does not extract a thumbnail or icon.
	Win8CropToSquare = 0x20,    // Introduced in Windows 8. If necessary, crop the bitmap to a square.
	Win8WideThumbnails = 0x40,  // Introduced in Windows 8. Stretch and crop the bitmap to a 0.7 aspect ratio.
	Win8IconBackground = 0x80,  // Introduced in Windows 8. If returning an icon, paint a background using the associated app's registered background color.
	Win8ScaleUp = 0x100         // Introduced in Windows 8. If necessary, stretch the bitmap so that the height and width fit the given size.
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory : IShellItem {
	[PreserveSig]
	int GetImage([In, MarshalAs(UnmanagedType.Struct)] Win32Interop.SizeW size, [In] ThumbnailOptions flags, [Out] out IntPtr phbm);
}