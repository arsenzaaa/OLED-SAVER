using System;
using System.Drawing;

#nullable disable

namespace OLEDSaver
{
    internal readonly struct OverlayRegionState : IEquatable<OverlayRegionState>
    {
        private OverlayRegionState(Rectangle cutoutRect, Size overlaySize, bool roundedCorners)
        {
            CutoutRect = cutoutRect;
            OverlaySize = overlaySize;
            RoundedCorners = roundedCorners;
        }

        public Rectangle CutoutRect { get; }
        public Size OverlaySize { get; }
        public bool RoundedCorners { get; }

        public static OverlayRegionState Create(Rectangle relativeRect, Size overlaySize, bool roundedCorners)
        {
            Rectangle overlayBounds = new Rectangle(Point.Empty, overlaySize);
            Rectangle cutoutRect = relativeRect.Width > 0 &&
                                   relativeRect.Height > 0 &&
                                   overlayBounds.IntersectsWith(relativeRect)
                ? relativeRect
                : Rectangle.Empty;

            return new OverlayRegionState(
                cutoutRect,
                overlaySize,
                !cutoutRect.IsEmpty && roundedCorners);
        }

        public bool Equals(OverlayRegionState other)
        {
            return CutoutRect.Equals(other.CutoutRect) &&
                   OverlaySize.Equals(other.OverlaySize) &&
                   RoundedCorners == other.RoundedCorners;
        }

        public override bool Equals(object obj)
        {
            return obj is OverlayRegionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CutoutRect, OverlaySize, RoundedCorners);
        }
    }
}
