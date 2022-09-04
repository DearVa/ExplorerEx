using System.Xml;

namespace SharpSvgImage.Svg
{
    public class ClipArtElement
    {
        public string Id { get; protected set; }

        public ClipArtElement(XmlNode node)
        {
            if (node == null)
                this.Id = "<null>";
            else
                this.Id = XmlUtil.AttrValue(node, "id");
        }
    }
}
