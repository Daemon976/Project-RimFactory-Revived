﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using ProjectRimFactory.Storage.Editables;
using UnityEngine;
using ProjectRimFactory.Storage.UI;
using System.Collections;
using ProjectRimFactory.Common.HarmonyPatches;

namespace ProjectRimFactory.Storage
{
    [StaticConstructorOnStartup]
    public abstract class Building_MassStorageUnit : Building_Storage, IHideItem, IHideRightClickMenu, IForbidPawnOutputItem, IForbidPawnInputItem
    {
        static readonly Texture2D RenameTex = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);

        List<Thing> items = new List<Thing>();
        List<Building_StorageUnitIOPort> ports = new List<Building_StorageUnitIOPort>();
        public string uniqueName;

        public abstract bool CanStoreMoreItems { get; }
        public IEnumerable<Thing> StoredItems => items;
        public int StoredItemsCount => items.Count;
        public override string LabelNoCount => uniqueName ?? base.LabelNoCount;
        public override string LabelCap => uniqueName ?? base.LabelCap;
        public virtual bool CanReceiveIO => true;

        public virtual bool HideItems => this.def.GetModExtension<DefModExtension_Crate>()?.hideItems ?? false;

        public virtual bool HideRightClickMenus => this.def.GetModExtension<DefModExtension_Crate>()?.hideRightClickMenus ?? false;

        public bool ForbidPawnAccess => this.def.GetModExtension<DefModExtension_Crate>()?.forbidPawnAccess ?? false;

        public virtual bool ForbidPawnInput => this.ForbidPawnAccess;

        public virtual bool ForbidPawnOutput => this.ForbidPawnAccess;

        public void DeregisterPort(Building_StorageUnitIOPort port)
        {
            ports.Remove(port);
        }
        public void RegisterPort(Building_StorageUnitIOPort port)
        {
            ports.Add(port);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;
            yield return new Command_Action
            {
                icon = RenameTex,
                action = () => Find.WindowStack.Add(new Dialog_RenameMassStorageUnit(this)),
                hotKey = KeyBindingDefOf.Misc1,
                defaultLabel = "PRFRenameMassStorageUnitLabel".Translate(),
                defaultDesc = "PRFRenameMassStorageUnitDesc".Translate()
            };
            yield return new Command_Action
            {
                icon = TexUI.RotRightTex,
                action = () =>
                {
                    RefreshStorage();
                    Messages.Message("PRFReorganize_Message".Translate(), MessageTypeDefOf.NeutralEvent);
                },
                defaultLabel = "PRFReorganize".Translate(),
                defaultDesc = "PRFReorganize_Desc".Translate()
            };
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            if (newItem.Position != Position)
            {
                RegisterNewItem(newItem);
            }
            RefreshStorage();
        }

        public virtual string GetUIThingLabel()
        {
            return "PRFMassStorageUIThingLabel".Translate(StoredItemsCount);
        }

        public virtual string GetITabString(int itemsSelected)
        {
            return "PRFItemsTabLabel".Translate(StoredItemsCount, itemsSelected);
        }

        public virtual void RegisterNewItem(Thing newItem)
        {
            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing item = things[i];
                if (item == newItem) continue;
                if (item.def.category == ThingCategory.Item && item.CanStackWith(newItem))
                {
                    item.TryAbsorbStack(newItem, true);
                }
                if (newItem.Destroyed)
                {
                    break;
                }
            }
            //Add a new stack of a thing
            if (!newItem.Destroyed)
            {
                

                if (!items.Contains(newItem))
                    items.Add(newItem);

                //What appens if its full?
                if (CanStoreMoreItems)
                {
                   
                    newItem.Position = Position;
                   

                }
                if (!newItem.Spawned)
                {
                    newItem.SpawnSetup(this.Map, false);
                }

            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ports, "ports", LookMode.Reference);
            Scribe_Values.Look(ref uniqueName, "uniqueName");
        }

        public override string GetInspectString()
        {
            string original = base.GetInspectString();
            StringBuilder stringBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(original))
            {
                stringBuilder.AppendLine(original);
            }
            stringBuilder.Append("PRF_TotalStacksNum".Translate(items.Count));
            return stringBuilder.ToString();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            List<Thing> thingsToSplurge = new List<Thing>(Position.GetThingList(Map));
            for (int i = 0; i < thingsToSplurge.Count; i++)
            {
                if (thingsToSplurge[i].def.category == ThingCategory.Item)
                {
                    thingsToSplurge[i].DeSpawn();
                    GenPlace.TryPlaceThing(thingsToSplurge[i], Position, Map, ThingPlaceMode.Near);
                }
            }
            base.DeSpawn();
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            items.Remove(newItem);
            RefreshStorage();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
           
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshStorage();
        }

        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();
            if (Current.CameraDriver.CurrentZoom <= CameraZoomRange.Close)
            {
                GenMapUI.DrawThingLabel(this, this.LabelCap + "\n\r" +  GetUIThingLabel()  );
            }
        }

        public void OutputItem(Thing item)
        {
            var outputCell = this.GetComp<ProjectRimFactory.Common.CompOutputAdjustable>()?.CurrentCell ?? this.Position + new IntVec3(0, 0, -2);
            GenPlace.TryPlaceThing(item.SplitOff(item.stackCount), outputCell, this.Map, ThingPlaceMode.Near);
        }

        public virtual void RefreshStorage()
        {
            items.Clear();
            if (!this.Spawned) return; // don't want to try getting lists of things when not on a map (see 155)
            foreach (IntVec3 cell in AllSlotCells())
            {
                List<Thing> things = new List<Thing>(cell.GetThingList(Map));
                for (int i = 0; i < things.Count; i++)
                {
                    Thing item = things[i];
                    if (item.def.category == ThingCategory.Item)
                    {
                        if (cell != Position)
                        {
                            RegisterNewItem(item);
                        }
                        else
                        {
                            if (!items.Contains(item))
                                items.Add(item);
                        }
                    }
                }
            }
            // Even though notifying I/O ports that the contents inside the storage unit have changed seems like a good idea, it can cause recursion issues.
            //for (int i = 0; i < ports.Count; i++)
            //{
            //    if (ports[i] == null)
            //    {
            //        ports.RemoveAt(i);
            //        i--;
            //    }
            //    else
            //    {
            //        ports[i].Notify_NeedRefresh();
            //    }
            //}
        }
    }
}
