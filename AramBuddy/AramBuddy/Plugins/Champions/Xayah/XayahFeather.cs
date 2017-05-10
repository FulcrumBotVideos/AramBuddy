using AramBuddy.MainCore.Common;
using EloBuddy;
using EloBuddy.SDK;
using SharpDX;
using Color = System.Drawing.Color;

namespace AramBuddy.Plugins.Champions.Xayah
{
    public class XayahFeather
    {
        public XayahFeather(Obj_AI_Minion feather)
        {
            this.Feather = feather;
            this.Position = feather.ServerPosition;
        }

        public bool IsValid => this.Feather != null && !this.Feather.IsDead;
        public Obj_AI_Minion Feather;
        public Vector3 Position;
        public Geometry.Polygon.Rectangle Rect => new Geometry.Polygon.Rectangle(this.Position, Player.Instance.ServerPosition, Xayah.E.SetSkillshot().Width);

        public bool WillHit(Obj_AI_Base target)
        {
            var time = Xayah.E.CastDelay + ((Position.Distance(target) / Xayah.E.SetSkillshot().Speed) * 1000);
            var predPos = target.PredictPosition((int)time);
            return this.Rect.IsInside(predPos);
        }

        public bool Draw()
        {
            this.Rect.Draw(Color.AliceBlue, 1);
            return true;
        }
    }
}
