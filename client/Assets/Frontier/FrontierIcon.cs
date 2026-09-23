using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Frontier
{
    /// Small vector vocabulary. No font glyph or emoji substitution.
    public sealed class FrontierIcon : VisualElement
    {
        public enum Symbol { Leaf, Shield, Flame, Wing }
        readonly Symbol _symbol;
        public FrontierIcon(Symbol symbol)
        {
            _symbol = symbol;
            AddToClassList("frontier-icon"); pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var size = contentRect.size;
            Vector2 P(float x, float y) => new Vector2(x * size.x / 24, y * size.y / 24);
            painter.fillColor = FrontierArt.Hex(_symbol == Symbol.Flame ? "#d78751" : _symbol == Symbol.Shield ? "#b78b3e" : "#619788");
            painter.strokeColor = FrontierArt.Hex("#302d40"); painter.lineWidth = 1.5f;
            painter.BeginPath();
            if (_symbol == Symbol.Shield)
            { painter.MoveTo(P(4,5)); painter.LineTo(P(12,2)); painter.LineTo(P(20,5)); painter.LineTo(P(18,15)); painter.LineTo(P(12,22)); painter.LineTo(P(6,15)); }
            else if (_symbol == Symbol.Flame)
            { painter.MoveTo(P(12,2)); painter.LineTo(P(14,10)); painter.LineTo(P(18,7)); painter.BezierCurveTo(P(26,23),P(1,27),P(5,13)); painter.LineTo(P(9,16)); }
            else if (_symbol == Symbol.Wing)
            { painter.MoveTo(P(3,17)); painter.LineTo(P(22,3)); painter.LineTo(P(19,13)); painter.LineTo(P(14,12)); painter.LineTo(P(13,18)); painter.LineTo(P(7,17)); painter.LineTo(P(5,22)); }
            else
            { painter.MoveTo(P(4,20)); painter.BezierCurveTo(P(0,5),P(15,3),P(21,3)); painter.BezierCurveTo(P(21,18),P(12,24),P(4,20)); }
            painter.ClosePath(); painter.Fill(); painter.Stroke();
            painter.BeginPath(); painter.MoveTo(P(8,17)); painter.LineTo(P(15,8)); painter.Stroke();
        }
    }
}
