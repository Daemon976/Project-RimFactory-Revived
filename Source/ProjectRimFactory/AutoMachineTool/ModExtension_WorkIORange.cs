﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using static ProjectRimFactory.AutoMachineTool.Ops;

namespace ProjectRimFactory.AutoMachineTool
{
    public class ModExtension_WorkIORange : DefModExtension
    {
        public int maxPower = 0;
        public int minPower = 0;

        public Type targetCellResolverType;
        private ITargetCellResolver targetCellResolver;
        public ITargetCellResolver TargetCellResolver
        {
            get
            {
                if (targetCellResolverType == null)
                {
                    return null;
                }
                if (targetCellResolver == null)
                {
                    this.targetCellResolver = (ITargetCellResolver)Activator.CreateInstance(targetCellResolverType);
                    this.targetCellResolver.Parent = this;
                }
                return this.targetCellResolver;
            }
        }

        public Type outputCellResolverType;
        private IOutputCellResolver outputCellResolver;
        public IOutputCellResolver OutputCellResolver
        {
            get
            {
                if (outputCellResolverType == null)
                {
                    return null;
                }
                if (outputCellResolver == null)
                {
                    this.outputCellResolver = (IOutputCellResolver)Activator.CreateInstance(outputCellResolverType);
                    this.outputCellResolver.Parent = this;
                }
                return this.outputCellResolver;
            }
        }

        public Type inputCellResolverType;
        private IInputCellResolver inputCellResolver;
        public IInputCellResolver InputCellResolver
        {
            get
            {
                if (inputCellResolverType == null)
                {
                    return null;
                }
                if (inputCellResolver == null)
                {
                    this.inputCellResolver = (IInputCellResolver)Activator.CreateInstance(inputCellResolverType);
                    this.inputCellResolver.Parent = this;
                }
                return this.inputCellResolver;
            }
        }

        public Color GetCellPatternColor(CellPattern pat)
        {
            switch (pat)
            {
                case CellPattern.BlurprintMin:
                    return this.blueprintMin;
                case CellPattern.BlurprintMax:
                    return this.blueprintMax;
                case CellPattern.Instance:
                    return this.instance;
                case CellPattern.OtherInstance:
                    return this.otherInstance;
                case CellPattern.OutputCell:
                    return this.outputCell;
                case CellPattern.OutputZone:
                    return this.outputZone;
                case CellPattern.InputCell:
                    return this.inputCell;
                case CellPattern.InputZone:
                    return this.inputZone;
            }
            return Color.white;
        }

        public Color blueprintMin = Color.white;
        public Color blueprintMax = Color.gray.A(0.6f);
        public Color instance = Color.white;
        public Color otherInstance = Color.white.A(0.35f);
        public Color inputCell = Color.white;
        public Color inputZone = Color.white.A(0.5f);
        public Color outputCell = Color.yellow;
        public Color outputZone = Color.yellow.A(0.5f);
    }

    public interface IInputCellResolver
    {
        Option<IntVec3> InputCell(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot);
        IEnumerable<IntVec3> InputZoneCells(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot);
        ModExtension_WorkIORange Parent { get; set; }
        Color GetColor(IntVec3 cell, Map map, Rot4 rot, CellPattern cellPattern);
    }

    public interface IOutputCellResolver
    {
        Option<IntVec3> OutputCell(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot);
        IEnumerable<IntVec3> OutputZoneCells(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot);
        ModExtension_WorkIORange Parent { get; set; }
        Color GetColor(IntVec3 cell, Map map, Rot4 rot, CellPattern cellPattern);
    }

    public class OutputCellResolver : IOutputCellResolver
    {
        public ModExtension_WorkIORange Parent { get; set; }

        public virtual Option<IntVec3> OutputCell(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot)
        {
            return Option(FacingCell(center, size, rot.Opposite));
        }

        private static readonly List<IntVec3> EmptyList = new List<IntVec3>();

        public virtual IEnumerable<IntVec3> OutputZoneCells(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot)
        {
            return this.OutputCell(def, center, size, map, rot).Select(c => c.SlotGroupCells(map)).GetOrDefault(EmptyList);
        }

        public virtual Color GetColor(IntVec3 cell, Map map, Rot4 rot, CellPattern cellPattern)
        {
            return this.Parent.GetCellPatternColor(cellPattern);
        }
    }

    public class ProductOutputCellResolver : OutputCellResolver
    {
        public override Option<IntVec3> OutputCell(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot)
        {
            return center.GetThingList(map)
                .Where(t => t.def == def)
                .OfType<IProductOutput>()
                .FirstOption()
                .Select(b => b.OutputCell());
        }
    }

    public interface ITargetCellResolver
    {
        IEnumerable<IntVec3> GetRangeCells(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot, int range);
        Color GetColor(IntVec3 cell, Map map, Rot4 rot, CellPattern cellPattern);
        ModExtension_WorkIORange Parent { get; set; }
        int GetRange(float power);
        bool NeedClearingCache { get; }
    }

    public static class ITargetCellResolverExtension
    {
        public static int MaxRange(this ITargetCellResolver r)
        {
            return r.GetRange(r.Parent.maxPower);
        }

        public static int MinRange(this ITargetCellResolver r)
        {
            return r.GetRange(r.Parent.minPower);
        }
    }

    public abstract class BaseTargetCellResolver : ITargetCellResolver
    {
        public abstract bool NeedClearingCache { get; }
        public ModExtension_WorkIORange Parent { get; set; }

        public virtual int GetRange(float power)
        {
            return Mathf.RoundToInt(power / 500) + 3;
        }

        public virtual Color GetColor(IntVec3 cell, Map map, Rot4 rot, CellPattern cellPattern)
        {
            return this.Parent.GetCellPatternColor(cellPattern);
        }

        public abstract IEnumerable<IntVec3> GetRangeCells(ThingDef def, IntVec3 center, IntVec2 size, Map map, Rot4 rot, int range);
    }

    public enum CellPattern {
        BlurprintMin,
        BlurprintMax,
        Instance,
        OtherInstance,
        OutputCell,
        OutputZone,
        InputCell,
        InputZone,
    }
}
