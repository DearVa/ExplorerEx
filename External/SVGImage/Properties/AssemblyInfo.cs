using System.Windows;
using System.Windows.Markup;

//In order to begin building localizable applications, set 
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page, 
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page, 
                                              // app, or any theme specific resource dictionaries)
)]

[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "SVGImage.SVG")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "SVGImage.SVG.Shapes")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "DotNetProjects.SVGImage.SVG.Shapes.Filter")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "SVGImage.SVG.PaintServer")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "DotNetProjects.SVGImage.SVG.FileLoaders")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "SVGImage.SVG")]
[assembly: XmlnsDefinition("https://github.com/dotnetprojects/SVGImage", "DotNetProjects.SVGImage.SVG.Animation")]

[assembly: XmlnsPrefix("https://github.com/dotnetprojects/SVGImage", "svg")]
