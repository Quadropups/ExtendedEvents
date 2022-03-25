namespace ExtendedEvents {

    /// <summary>Attribute used to set ColorField settings for Color parameter</summary>
    public sealed class ColorUsageAttribute : ArgumentAttribute {
        public readonly bool showAlpha = true;
        public readonly bool hdr = false;

        public ColorUsageAttribute(bool showAlpha) {
            this.showAlpha = showAlpha;
        }

        public ColorUsageAttribute(bool showAlpha, bool hdr) {
            this.showAlpha = showAlpha;
            this.hdr = hdr;
        }

    }
}