namespace ExplorerEx.Shell32;

// ReSharper disable once InconsistentNaming
public enum CSIDL : short {
	/// <summary>
	/// &lt;desktop&gt;
	/// </summary>
	Desktop = 0x0000,
	/// <summary>
	/// Internet Explorer (icon on desktop)
	/// </summary>
	Internet = 0x0001,
	/// <summary>
	/// Start Menu\Programs
	/// </summary>
	Programs = 0x0002,
	/// <summary>
	/// My Computer\Control Panel
	/// </summary>
	Controls = 0x0003,
	/// <summary>
	/// My Computer\Printers
	/// </summary>
	Printers = 0x0004,
	/// <summary>
	/// My Documents
	/// </summary>
	Personal = 0x0005,
	/// <summary>
	/// &lt;user name&gt;\Favorites
	/// </summary>
	Favorites = 0x0006,
	/// <summary>
	/// Start Menu\Programs\Startup
	/// </summary>
	Startup = 0x0007,
	/// <summary>
	/// &lt;user name&gt;\Recent
	/// </summary>
	Recent = 0x0008,
	/// <summary>
	/// &lt;user name&gt;\SendTo
	/// </summary>
	SendTo = 0x0009,
	/// <summary>
	/// &lt;desktop&gt;\Recycle Bin
	/// </summary>
	BitBucket = 0x000a,
	/// <summary>
	/// &lt;user name&gt;\Start Menu
	/// </summary>
	StartMenu = 0x000b,
	/// <summary>
	/// logical "My Documents" desktop icon
	/// </summary>
	MyDocuments = 0x000c,
	/// <summary>
	/// "My Music" folder
	/// </summary>
	MyMusic = 0x000d,
	/// <summary>
	/// "My Videos" folder
	/// </summary>
	MyVideo = 0x000e,
	/// <summary>
	/// &lt;user name&gt;\Desktop
	/// </summary>
	DesktopDirectory = 0x0010,
	/// <summary>
	/// My Computer
	/// </summary>
	Drives = 0x0011,
	/// <summary>
	/// Network Neighborhood (My Network Places)
	/// </summary>
	Network = 0x0012,
	/// <summary>
	/// &lt;user name&gt;\nethood
	/// </summary>
	Nethood = 0x0013,
	/// <summary>
	/// windows\fonts
	/// </summary>
	Fonts = 0x0014,
	Templates = 0x0015,
	/// <summary>
	/// All Users\Start Menu
	/// </summary>
	CommonStartMenu = 0x0016,
	/// <summary>
	/// All Users\Start Menu\Programs
	/// </summary>
	CommonPrograms = 0X0017,
	/// <summary>
	/// All Users\Startup
	/// </summary>
	CommonStartup = 0x0018,
	/// <summary>
	/// All Users\Desktop
	/// </summary>
	CommonDesktopDirectory = 0x0019,
	/// <summary>
	/// &lt;user name>\Application Data
	/// </summary>
	AppData = 0x001a,
	/// <summary>
	/// &lt;user name>\PrintHood
	/// </summary>
	PrintHood = 0x001b,

	/// <summary>
	/// &lt;user name>\Local Settings\Application Data (non roaming)
	/// </summary>
	LocalAppData = 0x001c,

	/// <summary>
	/// non localized startup
	/// </summary>
	AltStartup = 0x001d,
	/// <summary>
	/// non localized common startup
	/// </summary>
	CommonAltStartup = 0x001e,
	CommonFavorites = 0x001f,

	InternetCache = 0x0020,
	Cookies = 0x0021,
	History = 0x0022,
	/// <summary>
	/// All Users\Application Data
	/// </summary>
	CommonAppdata = 0x0023,
	/// <summary>
	/// GetWindowsDirectory()
	/// </summary>
	Windows = 0x0024,
	/// <summary>
	/// GetSystemDirectory()
	/// </summary>
	System = 0x0025,
	/// <summary>
	/// C:\Program Files
	/// </summary>
	ProgramFiles = 0x0026,
	/// <summary>
	/// C:\Program Files\My Pictures
	/// </summary>
	MyPictures = 0x0027,

	/// <summary>
	/// USERPROFILE
	/// </summary>
	Profile = 0x0028,
	/// <summary>
	/// x86 system directory on RISC
	/// </summary>
	SystemX86 = 0x0029,
	/// <summary>
	/// x86 C:\Program Files on RISC
	/// </summary>
	ProgramFilesX86 = 0x002a,

	/// <summary>
	/// C:\Program Files\Common
	/// </summary>
	ProgramFilesCommon = 0x002b,

	/// <summary>
	/// x86 Program Files\Common on RISC
	/// </summary>
	ProgramFilesCommonX86 = 0x002c,
	/// <summary>
	/// All Users\Templates
	/// </summary>
	CommonTemplates = 0x002d,

	/// <summary>
	/// All Users\Documents
	/// </summary>
	CommonDocuments = 0x002e,
	/// <summary>
	/// All Users\Start Menu\Programs\Administrative Tools
	/// </summary>
	CommonAdminTools = 0x002f,
	/// <summary>
	/// &lt;user name&gt;\Start Menu\Programs\Administrative Tools
	/// </summary>
	AdminTools = 0x0030,

	/// <summary>
	/// Network and Dial-up Connections
	/// </summary>
	Connections = 0x0031,
	/// <summary>
	/// All Users\My Music
	/// </summary>
	CommonMusic = 0x0035,
	/// <summary>
	/// All Users\My Pictures
	/// </summary>
	CommonPictures = 0x0036,
	/// <summary>
	/// All Users\My Video
	/// </summary>
	CommonVideo = 0x0037,

	/// <summary>
	/// USERPROFILE\Local Settings\Application Data\Microsoft\CD Burning
	/// </summary>
	CdBurnArea = 0x003b
}
