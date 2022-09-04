namespace SharpSvgImage.Svg
{
    public partial class XmlUtil
    {
        public class StyleItem
        {
            public string Name { get; set; }

            public string Value { get; set; }

            public StyleItem(string name, string value)
            {
                this.Name = name;
                this.Value = value;
            }
        }
    }
}