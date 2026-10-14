using System;
using System.Globalization;
using System.Text;

namespace Dashed
{
    public enum FailureKind { None, Parse, Structure, Prerequisite, Alignment }

    [Serializable]
    public struct ValidationResult
    {
        public bool success;
        public string message;
        public string code;
        public FailureKind kind;
        public int failureIndex;

        public static ValidationResult Pass(string text)
        {
            return new ValidationResult { success = true, message = text, code = "ok", failureIndex = -1 };
        }

        public static ValidationResult Fail(string code, string text, FailureKind kind = FailureKind.Structure, int index = -1)
        {
            return new ValidationResult { success = false, message = text, code = code, kind = kind, failureIndex = index };
        }
    }

    /// <summary>Rules use metres/degrees and the delivered Blender skeleton parameters, never image pixels.</summary>
    public static class Rules
    {
        public const float PisaExistingTilt = 5.5f;
        public const float PisaMaxOffset = 0.4f;
        public const float ColumnSpacing = 4.3f;
        public const float ColumnTolerance = 0.9f;
        public const float DrumCaptureRadius = 0.5f;
        public const int MaxStrokesPerRound = 5;
        public const int MissingColumns = 8;
        public const int DrumCount = 16;
        public const float MaxAdjacentHeightDifference = 1.5f;
        public const float MirrorCentralClearThreshold = 0.98f;
        const double DegreesToRadians = Math.PI / 180.0;
        // Absorbs float representation at inclusive physical thresholds, not visible extra tolerance.
        const double FloatEpsilon = 0.000001;

        public static bool IsFinite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }

        public static ValidationResult CheckAlignment(float errorDegrees)
        {
            if (!IsFinite(errorDegrees) || Math.Abs(errorDegrees) >= 3f)
                return ValidationResult.Fail("alignment", "画页尚未重合：角度误差必须小于 3°。", FailureKind.Alignment);
            return ValidationResult.Pass("页与建筑重合，可以落笔。");
        }

        public static int NewStoneTone(float errorDegrees)
        {
            if (!CheckAlignment(errorDegrees).success) return 0;
            return Math.Abs(errorDegrees) < 1f ? 1 : 2;
        }

        public static float PisaTiltAtLevel(int level, int k, float delta)
        {
            return PisaExistingTilt - Math.Max(0, level - k + 1) * delta;
        }

        public static float PisaOffsetAtLevel(int level, int k, float delta)
        {
            double theta = PisaTiltAtLevel(level, k, delta) * DegreesToRadians;
            double previous = PisaTiltAtLevel(level - 1, k, delta) * DegreesToRadians;
            double height = level == 8 ? 7.5 : 6.0;
            return (float)(height * Math.Abs(Math.Tan(theta) - Math.Tan(previous)));
        }

        public static ValidationResult CheckPisa(int k, float delta, bool includeBelfry = true)
        {
            int last = includeBelfry ? 8 : 7;
            if (!IsFinite(delta) || k < 4 || k > last)
                return ValidationResult.Fail("axis_parse", "轴线要从第三层檐口接起，起弯层必须落在续建层内。", FailureKind.Parse);
            if (delta <= 0f)
                return ValidationResult.Fail("bend_direction", "我画错了。往回修一点：续建轴线必须朝铅垂线回弯。");
            for (int level = 4; level <= last; level++)
            {
                float tilt = PisaTiltAtLevel(level, k, delta);
                float offset = PisaOffsetAtLevel(level, k, delta);
                if (Math.Abs(tilt) >= 80f || !IsFinite(offset) || offset > PisaMaxOffset + FloatEpsilon)
                    return ValidationResult.Fail("bearing_offset", "第 " + level + " 层错位超过 0.4 m，显影到这一层散成铅笔线。", FailureKind.Structure, level);
            }
            if (includeBelfry && Math.Abs(PisaTiltAtLevel(8, k, delta)) > 2f + FloatEpsilon)
                return ValidationResult.Fail("belfry_tilt", "钟室倾斜超过 2°，七口钟撞在框上，页还不能合上。", FailureKind.Structure, 8);
            return ValidationResult.Pass(includeBelfry ? "轴线往回弯，层层承重，七口钟挂正。" : "四层柱廊已站住。图签：停在第七层。");
        }

        public static ValidationResult CheckColumn(float offset, float tilt)
        {
            if (!IsFinite(offset) || !IsFinite(tilt) || Math.Abs(tilt) > 3f + FloatEpsilon)
                return ValidationResult.Fail("column_parse", "斜柱超过 3°，只能留下灰色草稿。", FailureKind.Parse);
            if (Math.Abs(offset) > ColumnTolerance + FloatEpsilon)
                return ValidationResult.Fail("column_rhythm", "柱距没有对上原物额枋：偏移须在 ±0.9 m 内。原物额枋仍留在地上。");
            return ValidationResult.Pass("柱距与垂直度有效，可以承接原物额枋。");
        }

        public static ValidationResult CheckPantheonAperture(float diameter, float offset)
        {
            if (!IsFinite(diameter) || !IsFinite(offset) || diameter <= 0)
                return ValidationResult.Fail("aperture_parse", "擦口没有形成可解析的眼洞。", FailureKind.Parse);
            if (Math.Abs(offset) > 1.5f + FloatEpsilon)
                return ValidationResult.Fail("aperture_center", "擦口偏离穹顶顶点超过 1.5 m，裂缝指向真正的顶点。");
            if (diameter < 6f - FloatEpsilon || diameter > 9f + FloatEpsilon)
                return ValidationResult.Fail("aperture_diameter", "眼洞直径必须在 6–9 m 内，压力环才能站住。");
            return ValidationResult.Pass("眼洞与压力环有效。第一次落笔只会落雨。");
        }

        /// <param name="sunMinutes">Signed minute offset from noon; inclusive [-20,20].</param>
        public static ValidationResult CheckPantheon(float diameter, float offset, int sunMinutes)
        {
            ValidationResult aperture = CheckPantheonAperture(diameter, offset);
            if (!aperture.success) return aperture;
            if (sunMinutes < -20 || sunMinutes > 20)
                return ValidationResult.Fail("sun_target", "太阳印章偏离正午，光斑没有照到门槛的 ×。");
            return ValidationResult.Pass("眼洞居中，直径有效，光斑照到门槛。");
        }

        /// <param name="openness">Erased projected spire area / closed outline, range 0..1.</param>
        public static ValidationResult CheckCologne(float[] deviations, float openness, bool hoisted)
        {
            if (deviations == null || deviations.Length != 3)
                return ValidationResult.Fail("contour_parse", "轮廓须经过三条参考线。", FailureKind.Parse);
            float tolerance = hoisted ? 4f : 2f;
            for (int i = 0; i < 3; i++)
            {
                if (!IsFinite(deviations[i])) return ValidationResult.Fail("contour_parse", "轮廓未能解析。", FailureKind.Parse, i);
                if (Math.Abs(deviations[i]) > tolerance + FloatEpsilon)
                    return ValidationResult.Fail("tower_symmetry", "第 " + (i + 1) + " 条参考线的镜像偏差超过 " + tolerance + " m，这一段散线。", FailureKind.Structure, i);
            }
            if (!hoisted)
            {
                if (!IsFinite(openness) || openness < 0 || openness > 1)
                    return ValidationResult.Fail("erasure_parse", "擦除遮罩没有形成有效镂空。", FailureKind.Parse);
                if (openness < 0.45f - FloatEpsilon)
                    return ValidationResult.Fail("spire_heavy", "尖顶是空的。镂空不足 45%，实心的下面撑不住。");
                if (openness > 0.85f + FloatEpsilon)
                    return ValidationResult.Fail("spire_ribs", "镂空超过 85%，只剩轮廓没有肋，塔尖垮下一半。");
            }
            return ValidationResult.Pass(hoisted ? "原物残片已成为锚点：镂空有效，对称容差放宽到 4 m。" : "三条参考线对称，塔尖镂空有效。");
        }

        public static ValidationResult CheckSagrada(float[] heights, int[] fragments, bool mirrorClear)
        {
            if (!mirrorClear) return ValidationResult.Fail("mirror_dust", "先擦净镜子中央，才能从取景框里看清拱顶。", FailureKind.Prerequisite);
            if (heights == null || heights.Length != 3 || fragments == null || fragments.Length != 3)
                return ValidationResult.Fail("span_parse", "三跨都要挂上链子。", FailureKind.Parse);
            for (int i = 0; i < 3; i++)
            {
                if (!IsFinite(heights[i]) || heights[i] <= 0)
                    return ValidationResult.Fail("span_missing", "第 " + (i + 1) + " 跨链子还没有挂上。", FailureKind.Prerequisite, i);
                if (fragments[i] < 1)
                    return ValidationResult.Fail("fragment_missing", "碎片是他留下的，别绕开它：第 " + (i + 1) + " 跨至少挂一块。", FailureKind.Structure, i);
            }
            for (int i = 0; i < 2; i++)
            {
                if (Math.Abs(heights[i + 1] - heights[i]) > MaxAdjacentHeightDifference + FloatEpsilon)
                    return ValidationResult.Fail("span_height", "第 " + (i + 1) + "、" + (i + 2) + " 跨净高差超过 1.5 m，接头留下一道虚线缝。", FailureKind.Structure, i);
            }
            return ValidationResult.Pass("三跨相邻净高接齐，每跨都有原物碎片。可以走进镜中的中殿。");
        }

        public static ValidationResult CheckFinalPage() { return ValidationResult.Pass("这一面我没看到。你去看。终页没有结构判定。"); }
    }

    [Serializable]
    public class PisaCandidate
    {
        public int k;
        public float delta;
        public bool includeBelfry;
        public string label;
        public PisaCandidate(int k, float delta, bool belfry, string label)
        { this.k = k; this.delta = delta; includeBelfry = belfry; this.label = label; }
    }

    [Serializable]
    public class PantheonCandidate
    {
        public float diameter, offset;
        public string label;
        public PantheonCandidate(float diameter, float offset, string label)
        { this.diameter = diameter; this.offset = offset; this.label = label; }
    }

    [Serializable]
    public class CologneCandidate
    {
        public float crayonMix;
        public string label;
        public CologneCandidate(float mix, string label) { crayonMix = mix; this.label = label; }
    }

    [Serializable]
    public class SagradaCandidate
    {
        public float[] heights;
        public float relativeVolume;
        public string label;
        public SagradaCandidate(float[] heights, float volume, string label)
        { this.heights = heights; relativeVolume = volume; this.label = label; }
    }

    /// <summary>Offline papers carry parameters. UI picks a paper, then Rules validates that paper.</summary>
    public static class DemoCandidates
    {
        // A misses belfry leveling; B passes; C exceeds bearing offset. They are different skeletons.
        public static readonly PisaCandidate[] Pisa = {
            new PisaCandidate(5, 0.7f, true, "起弯第 5 层 · 每层 0.7° · 钟室 2.7°"),
            new PisaCandidate(5, 1.4f, true, "起弯第 5 层 · 每层 1.4° · 钟室 −0.1°"),
            new PisaCandidate(7, 3.8f, true, "起弯第 7 层 · 每层 3.8° · 钟室 −2.1°")
        };
        public static readonly PantheonCandidate[] Pantheon = {
            new PantheonCandidate(5.8f, 0, "圆心 0.0 m · 直径 5.8 m"),
            new PantheonCandidate(8.2f, 0, "圆心 0.0 m · 直径 8.2 m"),
            new PantheonCandidate(9.2f, 0, "圆心 0.0 m · 直径 9.2 m")
        };
        // This one page explicitly shares geometry across all three papers; only handwriting differs.
        public static readonly CologneCandidate[] Cologne = {
            new CologneCandidate(0, "蜡笔 0%"), new CologneCandidate(0.3f, "蜡笔 30%"), new CologneCandidate(0.6f, "蜡笔 60%")
        };
        // Actual delivered fixed-L load variants SAG_17/18/19, not three forced arbitrary heights.
        public static readonly SagradaCandidate[] Sagrada = {
            new SagradaCandidate(new float[] {44f,44.473098f,44.2f}, 0.7f, "补全更小 · 净高 44 / 44.47 / 44.20 m"),
            new SagradaCandidate(new float[] {44f,44.5f,44.2f}, 1f, "补全原量 · 净高 44 / 44.50 / 44.20 m"),
            new SagradaCandidate(new float[] {44f,44.532708f,44.2f}, 1.3f, "补全更长 · 净高 44 / 44.53 / 44.20 m")
        };
    }

    /// <summary>
    /// Plain serializable state. Presentation decides when to show/place the preset drawing.
    /// Mutations never erase previously completed columns/spans after a failed attempt.
    /// </summary>
    [Serializable]
    public class DemoProgress
    {
        public int currentScene;
        public bool[] completed = new bool[5];
        public int[] selectedPapers = new int[5];
        public int[] tone = new int[5];
        public int pisaK = 5;
        public float pisaDelta = 1.4f;
        public bool pisaBelfry = true;
        public bool[] columns = new bool[8];
        public float[] columnOffsets = new float[8];
        public int[] drumOwners = Filled(16, -1);
        public int parthenonRounds;
        public bool[] architraves = new bool[9];
        public bool route_open, page_closed, restoration_complete;
        public bool aperturePlaced;
        public float pantheonDiameter = 8.2f, pantheonOffset;
        public int drainCount, sunMinutes;
        public float waterHeight = 0.06f;
        public bool raining;
        public bool colognePageJoined, cologneErased, cologneHoisted, coverCrayon;
        public float cologneOpenness = 0.52f;
        public float[] cologneDeviations = new float[3];
        public float mirrorClearRatio;
        public bool[] chains = new bool[3];
        public float[] chainLengths = new float[3];
        public float[] sagradaHeights = new float[3];
        public int[] fragmentOwners = Filled(5, -1);
        public bool finalPageDrawn, finalPagePlaced, returnedToWorkshop;

        static int[] Filled(int count, int value)
        {
            int[] result = new int[count];
            for (int i = 0; i < count; i++) result[i] = value;
            return result;
        }

        public int ColumnCount { get { int count = 0; foreach (bool c in columns) if (c) count++; return count; } }
        public int UsedDrumCount { get { int count = 0; foreach (int owner in drumOwners) if (owner >= 0) count++; return count; } }
        public bool MirrorClear { get { return mirrorClearRatio >= Rules.MirrorCentralClearThreshold; } }

        public ValidationResult PlacePisa(PisaCandidate paper, float alignment)
        {
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            if (paper == null) return ValidationResult.Fail("paper_missing", "先选择一张硫酸纸。", FailureKind.Prerequisite);
            result = Rules.CheckPisa(paper.k, paper.delta, paper.includeBelfry);
            if (!result.success) return result;
            pisaK = paper.k; pisaDelta = paper.delta; pisaBelfry = paper.includeBelfry;
            completed[0] = true; tone[0] = Rules.NewStoneTone(alignment);
            return result;
        }

        /// <param name="slots">Missing-column indices 0..7 (delivered column numbers 06..13).</param>
        public ValidationResult PlaceColumns(int[] slots, float[] offsets, float[] tilts, float alignment)
        {
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            if (slots == null || offsets == null || tilts == null || slots.Length == 0 || slots.Length > Rules.MaxStrokesPerRound || offsets.Length != slots.Length || tilts.Length != slots.Length)
                return ValidationResult.Fail("stroke_count", "一次生成只能包含 1–5 根柱，补八根必须至少两轮。", FailureKind.Parse);
            bool[] seen = new bool[8];
            // Validate the complete batch before committing, so malformed arrays never partly mutate a save.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] < 0 || slots[i] >= 8 || seen[slots[i]]) return ValidationResult.Fail("column_slot", "本轮柱位重复或越界。", FailureKind.Parse);
                seen[slots[i]] = true;
                result = Rules.CheckColumn(offsets[i], tilts[i]);
                if (!result.success) return result;
            }
            for (int i = 0; i < slots.Length; i++) { columns[slots[i]] = true; columnOffsets[slots[i]] = offsets[i]; }
            parthenonRounds++; tone[1] = Rules.NewStoneTone(alignment);
            UpdateParthenon();
            return ValidationResult.Pass("本轮柱子已显影，原物额枋归位；补柱 " + ColumnCount + "/8，旧鼓 " + UsedDrumCount + "/16。");
        }

        public ValidationResult CaptureDrum(int drum, int column, float contactDistance, bool onSupportSurface = true)
        {
            if (drum < 0 || drum >= 16 || column < 0 || column >= 8) return ValidationResult.Fail("drum_id", "旧鼓或柱位编号无效。", FailureKind.Parse);
            if (drumOwners[drum] >= 0) return ValidationResult.Fail("drum_used", "这块旧鼓已经归位，不会复制第二块。", FailureKind.Prerequisite);
            if (!columns[column]) return ValidationResult.Fail("column_missing", "先立起这一根柱；推到台基上的旧鼓仍要等柱子接手。", FailureKind.Prerequisite);
            if (!Rules.IsFinite(contactDistance) || contactDistance < 0 || contactDistance > Rules.DrumCaptureRadius + 0.000001f || !onSupportSurface)
                return ValidationResult.Fail("drum_distance", "把旧鼓推到柱脚 0.5 m 内，并落在台基支承面上。", FailureKind.Prerequisite);
            drumOwners[drum] = column; UpdateParthenon();
            return ValidationResult.Pass("原物柱鼓被吃进柱身，新石保留浅白色。旧鼓 " + UsedDrumCount + "/16。");
        }

        void UpdateParthenon()
        {
            architraves[0] = columns[0]; architraves[8] = columns[7];
            for (int i = 1; i < 8; i++) architraves[i] = columns[i - 1] && columns[i];
            route_open = columns[0] && columns[1] && columns[2] && columns[3] && columns[4];
            page_closed = route_open && UsedDrumCount == 16;
            restoration_complete = ColumnCount == 8 && UsedDrumCount == 16;
            if (page_closed) completed[1] = true;
        }

        public ValidationResult PlacePantheon(PantheonCandidate paper, float alignment)
        {
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            if (paper == null) return ValidationResult.Fail("paper_missing", "先选择擦口的硫酸纸。", FailureKind.Prerequisite);
            result = Rules.CheckPantheonAperture(paper.diameter, paper.offset);
            if (!result.success) return result;
            pantheonDiameter = paper.diameter; pantheonOffset = paper.offset; tone[2] = Rules.NewStoneTone(alignment);
            if (!aperturePlaced) { aperturePlaced = true; raining = true; waterHeight = Math.Max(waterHeight, 0.06f); }
            return ValidationResult.Pass("眼洞已打开，雨落下来；画排水孔会立即生效，页面暂不合上。");
        }

        public ValidationResult AddDrain()
        {
            if (!aperturePlaced) return ValidationResult.Fail("rain_first", "先举页落笔，让雨从眼洞落下来。", FailureKind.Prerequisite);
            if (drainCount >= 4) return ValidationResult.Pass("四个演示排水孔已经全部打开。");
            drainCount++;
            return ValidationResult.Pass("排水孔 " + drainCount + "/4：收笔即生效，水开始退。");
        }

        /// <summary>Delivered game-scaled water balance; dryness causes rain to stop, never a fixed timer.</summary>
        public void TickPantheon(float deltaSeconds)
        {
            if (!aperturePlaced || !Rules.IsFinite(deltaSeconds) || deltaSeconds <= 0) return;
            double inflow = raining ? 0.008 : 0;
            double outflow = drainCount == 1 ? 0.004 : drainCount * 0.012;
            double floorArea = Math.PI * 21.65 * 21.65;
            waterHeight = (float)Math.Max(0, waterHeight + (inflow - outflow) * 300 * deltaSeconds / floorArea);
            if (waterHeight <= 0.003f) raining = false;
        }

        public ValidationResult TryClosePantheon()
        {
            if (!aperturePlaced) return ValidationResult.Fail("aperture_missing", "还没有显影眼洞。", FailureKind.Prerequisite);
            if (drainCount == 0 || waterHeight > 0.003f || raining)
                return ValidationResult.Fail("water_remaining", "积水把光斑反上穹顶；先让水退到地砖露出来。", FailureKind.Prerequisite);
            ValidationResult result = Rules.CheckPantheon(pantheonDiameter, pantheonOffset, sunMinutes);
            if (result.success) completed[2] = true;
            return result;
        }

        public ValidationResult PlaceCologne(float alignment)
        {
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            if (!colognePageJoined) return ValidationResult.Fail("page_half", "另一半在你那儿：先拼上封底口袋里的蜡笔画。", FailureKind.Prerequisite);
            if (!cologneErased && !cologneHoisted) return ValidationResult.Fail("spire_solid", "用橡皮擦出塔尖窗洞，或用吊索将原物残片吊成锚点。", FailureKind.Prerequisite);
            result = Rules.CheckCologne(cologneDeviations, cologneOpenness, cologneHoisted);
            if (result.success) { completed[3] = true; tone[3] = Rules.NewStoneTone(alignment); }
            return result;
        }

        public ValidationResult ClearMirror(float centralClearRatio)
        {
            if (!Rules.IsFinite(centralClearRatio) || centralClearRatio < 0 || centralClearRatio > 1)
                return ValidationResult.Fail("dust_mask", "镜面擦除遮罩无效。", FailureKind.Parse);
            mirrorClearRatio = Math.Max(mirrorClearRatio, centralClearRatio);
            return ValidationResult.Pass(MirrorClear ? "镜子中央已擦净，倒影露出来。" : "灰尘正在擦掉，中央还未清晰。");
        }

        /// <summary>Immediate-on-stroke chain fixture. Fragment IDs are 0..4, corresponding to F1..F5.</summary>
        public ValidationResult HangChain(int span, float length, float height, int[] capturedIds)
        {
            if (span < 0 || span >= 3 || !Rules.IsFinite(length) || !Rules.IsFinite(height) || height <= 0)
                return ValidationResult.Fail("chain_parse", "链子端点或弧长未能解析。", FailureKind.Parse);
            if (length < 1.5f) return ValidationResult.Fail("chain_short", "太短：弧长小于两挂钩距离，链子弹回虚线。", FailureKind.Parse, span);
            if (capturedIds == null) capturedIds = new int[0];
            bool[] seen = new bool[5];
            foreach (int id in capturedIds)
            {
                if (id < 0 || id >= 5 || seen[id]) return ValidationResult.Fail("fragment_id", "碎片编号无效或重复。", FailureKind.Parse);
                seen[id] = true;
                bool allowed = id < 3 ? id == span : (id == 3 ? span <= 1 : span >= 1);
                if (!allowed) return ValidationResult.Fail("fragment_span", "固定碎片不能带到别的跨，边界碎片只可进相邻跨。", FailureKind.Prerequisite);
                if (fragmentOwners[id] >= 0 && fragmentOwners[id] != span)
                    return ValidationResult.Fail("fragment_owned", "这块分界碎片已被另一跨先挂上，不能复制。", FailureKind.Prerequisite);
            }
            // Redrawing this span alone releases only its own fragments; all other successful spans stay.
            for (int i = 0; i < fragmentOwners.Length; i++) if (fragmentOwners[i] == span) fragmentOwners[i] = -1;
            foreach (int id in capturedIds) fragmentOwners[id] = span;
            chains[span] = true; chainLengths[span] = length; sagradaHeights[span] = height;
            return ValidationResult.Pass("第 " + (span + 1) + " 跨链子挂上，镜中拱顶立即立起；挂入 " + capturedIds.Length + " 块碎片。");
        }

        public int[] FragmentCounts()
        {
            int[] counts = new int[3];
            foreach (int owner in fragmentOwners) if (owner >= 0 && owner < 3) counts[owner]++;
            return counts;
        }

        public ValidationResult PlaceSagrada(float alignment)
        {
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            result = Rules.CheckSagrada(sagradaHeights, FragmentCounts(), MirrorClear);
            if (result.success) { completed[4] = true; tone[4] = Rules.NewStoneTone(alignment); }
            return result;
        }

        public ValidationResult PlaceFinalPage(float alignment)
        {
            if (!completed[4]) return ValidationResult.Fail("nave_incomplete", "先接齐三跨，走进中殿再翻到终页。", FailureKind.Prerequisite);
            if (!finalPageDrawn) return ValidationResult.Fail("final_blank", "在最后一个框里完成你的画。", FailureKind.Prerequisite);
            ValidationResult result = Rules.CheckAlignment(alignment);
            if (!result.success) return result;
            finalPagePlaced = true;
            return Rules.CheckFinalPage();
        }

        public ValidationResult AdvanceScene()
        {
            if (currentScene < 0 || currentScene > 4) return ValidationResult.Fail("scene_id", "场景编号无效。", FailureKind.Parse);
            if (!completed[currentScene]) return ValidationResult.Fail("page_open", "当前页还没有合上，先完成这一页的互动。", FailureKind.Prerequisite);
            if (currentScene < 4) currentScene++;
            return ValidationResult.Pass("门框显影，可以进入下一页。");
        }

        // Dependency-free persistence; all arrays retain stable object IDs and local successes.
        public string ToSaveString()
        {
            StringBuilder b = new StringBuilder("DashedProgress/1\n");
            Write(b, currentScene, completed, selectedPapers, tone, pisaK, pisaDelta, pisaBelfry,
                columns, columnOffsets, drumOwners, parthenonRounds, architraves, route_open,
                page_closed, restoration_complete, aperturePlaced, pantheonDiameter, pantheonOffset,
                drainCount, sunMinutes, waterHeight, raining, colognePageJoined, cologneErased,
                cologneHoisted, coverCrayon, cologneOpenness, cologneDeviations, mirrorClearRatio,
                chains, chainLengths, sagradaHeights, fragmentOwners, finalPageDrawn, finalPagePlaced, returnedToWorkshop);
            return b.ToString();
        }

        static void Write(StringBuilder b, params object[] values)
        {
            foreach (object value in values)
            {
                Array array = value as Array;
                if (array != null)
                {
                    for (int i = 0; i < array.Length; i++) { if (i > 0) b.Append(','); b.Append(Scalar(array.GetValue(i))); }
                }
                else b.Append(Scalar(value));
                b.Append('\n');
            }
        }

        static string Scalar(object value)
        {
            if (value is bool) return (bool)value ? "1" : "0";
            if (value is float) return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool TryFromSaveString(string data, out DemoProgress progress)
        {
            progress = null;
            if (data == null) return false;
            string[] v = data.Replace("\r", "").Split('\n');
            if (v.Length != 38 || v[0] != "DashedProgress/1" || v[37] != "") return false;
            try
            {
                DemoProgress p = new DemoProgress(); int i = 1;
                p.currentScene = Int(v[i++]); p.completed = Bools(v[i++],5); p.selectedPapers = Ints(v[i++],5); p.tone = Ints(v[i++],5);
                p.pisaK = Int(v[i++]); p.pisaDelta = Float(v[i++]); p.pisaBelfry = Bool(v[i++]);
                p.columns = Bools(v[i++],8); p.columnOffsets = Floats(v[i++],8); p.drumOwners = Ints(v[i++],16); p.parthenonRounds = Int(v[i++]);
                p.architraves = Bools(v[i++],9); p.route_open = Bool(v[i++]); p.page_closed = Bool(v[i++]); p.restoration_complete = Bool(v[i++]);
                p.aperturePlaced = Bool(v[i++]); p.pantheonDiameter = Float(v[i++]); p.pantheonOffset = Float(v[i++]); p.drainCount = Int(v[i++]);
                p.sunMinutes = Int(v[i++]); p.waterHeight = Float(v[i++]); p.raining = Bool(v[i++]); p.colognePageJoined = Bool(v[i++]);
                p.cologneErased = Bool(v[i++]); p.cologneHoisted = Bool(v[i++]); p.coverCrayon = Bool(v[i++]); p.cologneOpenness = Float(v[i++]);
                p.cologneDeviations = Floats(v[i++],3); p.mirrorClearRatio = Float(v[i++]); p.chains = Bools(v[i++],3);
                p.chainLengths = Floats(v[i++],3); p.sagradaHeights = Floats(v[i++],3); p.fragmentOwners = Ints(v[i++],5);
                p.finalPageDrawn = Bool(v[i++]); p.finalPagePlaced = Bool(v[i++]); p.returnedToWorkshop = Bool(v[i++]);
                if (p.currentScene < 0 || p.currentScene > 4 || p.parthenonRounds < 0 || p.drainCount < 0 || p.drainCount > 4 || p.waterHeight < 0 || p.mirrorClearRatio < 0 || p.mirrorClearRatio > 1) return false;
                foreach (int owner in p.drumOwners) if (owner < -1 || owner > 7 || (owner >= 0 && !p.columns[owner])) return false;
                foreach (int owner in p.fragmentOwners) if (owner < -1 || owner > 2) return false;
                foreach (int selected in p.selectedPapers) if (selected < 0 || selected > 2) return false;
                foreach (int t in p.tone) if (t < 0 || t > 2) return false;
                p.UpdateParthenon();
                progress = p; return true;
            }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentException) { return false; }
        }

        static int Int(string s) { return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        static float Float(string s)
        {
            float result = float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!Rules.IsFinite(result)) throw new FormatException();
            return result;
        }
        static bool Bool(string s) { if (s != "0" && s != "1") throw new FormatException(); return s == "1"; }
        static string[] Parts(string s, int length) { string[] p = s.Split(','); if (p.Length != length) throw new FormatException(); return p; }
        static int[] Ints(string s, int length) { string[] p = Parts(s,length); int[] r = new int[length]; for(int i=0;i<length;i++)r[i]=Int(p[i]); return r; }
        static float[] Floats(string s, int length) { string[] p = Parts(s,length); float[] r = new float[length]; for(int i=0;i<length;i++)r[i]=Float(p[i]); return r; }
        static bool[] Bools(string s, int length) { string[] p = Parts(s,length); bool[] r = new bool[length]; for(int i=0;i<length;i++)r[i]=Bool(p[i]); return r; }
    }
}
