using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Sentakki.Objects;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Sentakki.Beatmaps
{
    public class SentakkiBeatmapProcessor : BeatmapProcessor
    {
        public new SentakkiBeatmap Beatmap => (SentakkiBeatmap)((base.Beatmap is EditorBeatmap eb) ? eb.PlayableBeatmap : base.Beatmap);

        public Action<SentakkiBeatmap>? CustomNoteColouringDelegate = null;

        public SentakkiBeatmapProcessor(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        public override void PostProcess()
        {
            base.PostProcess();

            if (CustomNoteColouringDelegate is not null)
                CustomNoteColouringDelegate?.Invoke(Beatmap);
            else
                applyDefaultNoteColouring();
        }

        private IEnumerable<List<HitObject>> createTwinGroups()
        {
            var hitObjectsCategory = getColorableHitObject(Beatmap.HitObjects).OrderBy(ho => ho.StartTime + (ho is SlideBody s ? s.ShootDelay : 0)).GroupBy(h => new { isSlide = h is SlideBody });

            foreach (var hitobjectCategory in hitObjectsCategory)
            {
                List<HitObject> timedGroup = [];
                double lastTime = double.MinValue;

                foreach (var ho in hitobjectCategory)
                {
                    double time = ho.StartTime + (ho is SlideBody s ? s.ShootDelay : 0);

                    if ((time - lastTime) >= 1 && timedGroup.Count > 0)
                    {
                        yield return timedGroup;
                        timedGroup = [];
                    }

                    timedGroup.Add(ho);
                    lastTime = time;
                }

                if (timedGroup.Count > 0)
                    yield return timedGroup;
            }
        }

        private void applyDefaultNoteColouring()
        {
            Color4 twinColor = Color4.Gold;
            Color4 breakColor = Color4.OrangeRed;

            var hitObjectGroups = createTwinGroups();

            foreach (var group in hitObjectGroups)
            {
                bool isTwin = group.Count(countsForTwin) > 1; // This determines whether the twin colour should be used for eligible objects

                foreach (SentakkiHitObject hitObject in group)
                {
                    if (hitObject is TouchHold th)
                    {
                        th.ColourPalette = TouchHold.DefaultPalette;

                        if (th.Break)
                            th.ColourPalette = TouchHold.BreakPalette;
                        else if (isTwin)
                            th.ColourPalette = TouchHold.TwinPalette;

                        continue;
                    }

                    Color4 noteColor = hitObject.DefaultNoteColour;

                    if (hitObject is SentakkiHitObject laned && laned.Break)
                        noteColor = breakColor;
                    else if (isTwin)
                        noteColor = twinColor;

                    hitObject.NoteColour = noteColor;
                }
            }
        }

        private IEnumerable<HitObject> getColorableHitObject(IReadOnlyList<HitObject> hitObjects)
        {
            for (int i = 0; i < hitObjects.Count; ++i)
            {
                var hitObject = hitObjects[i];
                if (canBeColored(hitObject)) yield return hitObject;

                foreach (var nested in getColorableHitObject(hitObject.NestedHitObjects))
                    yield return nested;
            }
        }

        private static bool canBeColored(HitObject hitObject)
        {
            switch (hitObject)
            {
                case Tap:
                case SlideBody:
                case Hold.HoldHead:
                case Touch:
                case TouchHold:
                    return true;

                // HitObject lines take the parent colour, instead of considering the nested object's colour
                case Slide:
                case Hold:
                    return true;
            }
            return false;
        }

        private static bool countsForTwin(HitObject hitObject) => hitObject switch
        {
            Hold.HoldHead => false,
            Slide => false,
            _ => true
        };
    }
}
