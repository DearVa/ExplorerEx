using System.IO;

namespace DotNetProjects.SVGImage.SVG.FileLoaders
{
    public interface IExternalFileLoader
    {
        Stream LoadFile(string hRef, string svgFilename);
    }
}
