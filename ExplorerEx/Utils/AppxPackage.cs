using ExplorerEx.Shell32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Utils;

public class AppxPackage {
	private AppxPackage() { }

	public string FullName { get; private set; }
	public string FullPath { get; private set; }
	public string Publisher { get; private set; }
	public string PublisherId { get; private set; }
	public string ResourceId { get; private set; }
	public string FamilyName { get; private set; }
	public string ApplicationUserModelId { get; private set; }
	public string Logo { get; private set; }
	public string PublisherDisplayName { get; private set; }
	public string Description { get; private set; }
	public string DisplayName { get; private set; }
	public bool IsFramework { get; private set; }
	public Version Version { get; private set; }
	public AppxPackageArchitecture ProcessorArchitecture { get; private set; }

	public IEnumerable<AppxPackage> DependencyGraph {
		get {
			return QueryPackageInfo(FullName, PackageConstants.FilterAllLoaded).Where(p => p.FullName != FullName);
		}
	}

	public static string GetFullPathByPackageName(string fullName) {
		OpenPackageInfoByFullName(fullName, 0, out var pInfo);
		if (pInfo != IntPtr.Zero) {
			var infoBuffer = IntPtr.Zero;
			try {
				var len = 0;
				GetPackageInfo(pInfo, PackageConstants.InformationBasic, ref len, IntPtr.Zero, out _);
				if (len > 0) {
					infoBuffer = Marshal.AllocHGlobal(len);
					GetPackageInfo(pInfo, PackageConstants.InformationBasic, ref len, infoBuffer, out _);
					return Marshal.PtrToStringUni(((PackageInfo)Marshal.PtrToStructure(infoBuffer, typeof(PackageInfo))!).path);
				}
			} finally {
				if (infoBuffer != IntPtr.Zero) {
					Marshal.FreeHGlobal(infoBuffer);
				}
				ClosePackageInfo(pInfo);
			}
		}
		return null;
	}

	/// <summary>
	/// 找到大于给定大小的最大图片，例如给定大小为90，但是有10,30,80,100,120几种大小，就返回100的那张
	/// </summary>
	/// <param name="fullPath"></param>
	/// <param name="resourceName"></param>
	/// <param name="desireSize"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"></exception>
	public static string FindMinimumQualifiedImagePath(string fullPath, string resourceName, int desireSize) {
		if (resourceName == null) {
			throw new ArgumentNullException(nameof(resourceName));
		}

		// ReSharper disable once StringLiteralTypo
		const string sizeToken = ".targetsize-";
		var name = Path.GetFileNameWithoutExtension(resourceName);
		var ext = Path.GetExtension(resourceName);
		var nowSize = int.MaxValue;
		string result = null;
		foreach (var filePath in Directory.EnumerateFiles(Path.Combine(fullPath, Path.GetDirectoryName(resourceName)!), name + sizeToken + "*" + ext)) {
			var fileName = Path.GetFileNameWithoutExtension(filePath);
			var pos = fileName.IndexOf(sizeToken, StringComparison.Ordinal) + sizeToken.Length;
			var sizeText = fileName[pos..];
			if (int.TryParse(sizeText, out var actualSize) && actualSize >= desireSize && actualSize < nowSize) {
				nowSize = actualSize;
				result = filePath;
			}
		}
		return result;
	}

	public static IEnumerable<AppxPackage> QueryPackageInfo(string fullName, PackageConstants flags) {
		OpenPackageInfoByFullName(fullName, 0, out var pInfo);
		if (pInfo != IntPtr.Zero) {
			var infoBuffer = IntPtr.Zero;
			try {
				var len = 0;
				GetPackageInfo(pInfo, flags, ref len, IntPtr.Zero, out var count);
				if (len > 0) {
					// ReSharper disable once SuspiciousTypeConversion.Global
					var factory = (IAppxFactory)new AppxFactory();
					infoBuffer = Marshal.AllocHGlobal(len);
					var res = GetPackageInfo(pInfo, flags, ref len, infoBuffer, out count);
					for (var i = 0; i < count; i++) {
						var info = (PackageInfo)Marshal.PtrToStructure(infoBuffer + i * Marshal.SizeOf(typeof(PackageInfo)), typeof(PackageInfo))!;
						var package = new AppxPackage {
							FamilyName = Marshal.PtrToStringUni(info.packageFamilyName),
							FullName = Marshal.PtrToStringUni(info.packageFullName),
							FullPath = Marshal.PtrToStringUni(info.path),
							Publisher = Marshal.PtrToStringUni(info.packageId.publisher),
							PublisherId = Marshal.PtrToStringUni(info.packageId.publisherId),
							ResourceId = Marshal.PtrToStringUni(info.packageId.resourceId),
							ProcessorArchitecture = info.packageId.processorArchitecture,
							Version = new Version(info.packageId.VersionMajor, info.packageId.VersionMinor, info.packageId.VersionBuild, info.packageId.VersionRevision)
						};

						// read manifest
						//string manifestPath = System.IO.Path.Combine(package.FullPath, "AppXManifest.xml");
						//const int STGM_SHARE_DENY_NONE = 0x40;
						//IStream strm;
						//SHCreateStreamOnFileEx(manifestPath, STGM_SHARE_DENY_NONE, 0, false, IntPtr.Zero, out strm);
						//if (strm != null) {
						//    var reader = factory.CreateManifestReader(strm);
						//    package._properties = reader.GetProperties();
						//    package.Description = package.GetPropertyStringValue("Description");
						//    package.DisplayName = package.GetPropertyStringValue("DisplayName");
						//    package.Logo = package.GetPropertyStringValue("Logo");
						//    package.PublisherDisplayName = package.GetPropertyStringValue("PublisherDisplayName");
						//    package.IsFramework = package.GetPropertyBoolValue("Framework");

						//    var apps = reader.GetApplications();
						//    while (apps.GetHasCurrent()) {
						//        var app = apps.GetCurrent();
						//        var appx = new AppxApp(app);
						//        appx.Description = GetStringValue(app, "Description");
						//        appx.DisplayName = GetStringValue(app, "DisplayName");
						//        appx.EntryPoint = GetStringValue(app, "EntryPoint");
						//        appx.Executable = GetStringValue(app, "Executable");
						//        appx.Id = GetStringValue(app, "Id");
						//        appx.Logo = GetStringValue(app, "Logo");
						//        appx.SmallLogo = GetStringValue(app, "SmallLogo");
						//        appx.StartPage = GetStringValue(app, "StartPage");
						//        appx.Square150x150Logo = GetStringValue(app, "Square150x150Logo");
						//        appx.Square30x30Logo = GetStringValue(app, "Square30x30Logo");
						//        appx.BackgroundColor = GetStringValue(app, "BackgroundColor");
						//        appx.ForegroundText = GetStringValue(app, "ForegroundText");
						//        appx.WideLogo = GetStringValue(app, "WideLogo");
						//        appx.Wide310x310Logo = GetStringValue(app, "Wide310x310Logo");
						//        appx.ShortName = GetStringValue(app, "ShortName");
						//        appx.Square310x310Logo = GetStringValue(app, "Square310x310Logo");
						//        appx.Square70x70Logo = GetStringValue(app, "Square70x70Logo");
						//        appx.MinWidth = GetStringValue(app, "MinWidth");
						//        package._apps.Add(appx);
						//        apps.MoveNext();
						//    }
						//    Marshal.ReleaseComObject(strm);
						//}
						yield return package;
					}
					Marshal.ReleaseComObject(factory);
				}
			} finally {
				if (infoBuffer != IntPtr.Zero) {
					Marshal.FreeHGlobal(infoBuffer);
				}
				ClosePackageInfo(pInfo);
			}
		}
	}
}