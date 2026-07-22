using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Vestibule
{
    public class CompProperties_EntryWardrobe : CompProperties
    {
        public CompProperties_EntryWardrobe()
        {
            compClass = typeof(CompEntryWardrobe);
        }
    }

    public class CompEntryWardrobe : ThingComp
    {
        public ApparelPolicy civilOutfit;
        public ApparelPolicy specialOutfit;
        public Building_Door civilDoor;
        public Building_Door specialDoor;

        private Dictionary<Pawn, IntVec3> lastPos = new Dictionary<Pawn, IntVec3>();
        private HashSet<Pawn> wasInSas = new HashSet<Pawn>();

        private static readonly HashSet<JobDef> exemptJobs = new HashSet<JobDef>
        {
            JobDefOf.BeatFire,
            JobDefOf.ExtinguishFiresNearby,
            JobDefOf.TendPatient,
            JobDefOf.Rescue,
            JobDefOf.DeliverToBed,
            JobDefOf.TakeWoundedPrisonerToBed,
            JobDefOf.CarryDownedPawnToExit,
            JobDefOf.CarryDownedPawnToPortal,
            JobDefOf.CarryDownedPawnDrafted,
            JobDefOf.CarryToCryptosleepCasket,
            JobDefOf.EscortPrisonerToBed,
            JobDefOf.CarryToPrisonerBedDrafted,
        };

        public CompProperties_EntryWardrobe Props => (CompProperties_EntryWardrobe)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref civilOutfit, "civilOutfit");
            Scribe_References.Look(ref specialOutfit, "specialOutfit");
            Scribe_References.Look(ref civilDoor, "civilDoor");
            Scribe_References.Look(ref specialDoor, "specialDoor");
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Map == null || civilDoor == null || specialDoor == null) return;

            Room sasRoom = RegionAndRoomQuery.RoomAt(parent.Position, parent.Map);
            if (sasRoom == null) return;

            foreach (var pawn in parent.Map.mapPawns.FreeColonistsSpawned.ToList())
            {
                IntVec3 prevPos = lastPos.TryGetValue(pawn, out var lp) ? lp : pawn.Position;
                bool wasIn = wasInSas.Contains(pawn);
                Room currentRoom = RegionAndRoomQuery.RoomAt(pawn.Position, pawn.Map);
                bool isIn = currentRoom == sasRoom;

                if (isIn && !wasIn)
                {
                    float distCivil = (prevPos - civilDoor.Position).LengthHorizontalSquared;
                    float distSpecial = (prevPos - specialDoor.Position).LengthHorizontalSquared;

                    ApparelPolicy target = distCivil < distSpecial ? specialOutfit : civilOutfit;
                    if (target != null && pawn.outfits != null)
                    {
                        pawn.outfits.CurrentApparelPolicy = target;
                        TryForceRedress(pawn);
                    }
                }

                if (isIn) wasInSas.Add(pawn); else wasInSas.Remove(pawn);
                lastPos[pawn] = pawn.Position;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "Assigner tenue civile",
                defaultDesc = civilOutfit != null ? $"Actuelle : {civilOutfit.label}" : "Aucune tenue assignée",
                icon = TexCommand.SelectShelf,
                action = () => OpenOutfitMenu(o => civilOutfit = o)
            };

            yield return new Command_Action
            {
                defaultLabel = "Assigner tenue spéciale",
                defaultDesc = specialOutfit != null ? $"Actuelle : {specialOutfit.label}" : "Aucune tenue assignée",
                icon = TexCommand.SelectShelf,
                action = () => OpenOutfitMenu(o => specialOutfit = o)
            };

            yield return new Command_Action
            {
                defaultLabel = "Sélectionner porte civile",
                defaultDesc = civilDoor != null ? $"Porte liée en {civilDoor.Position}" : "Aucune porte liée",
                icon = TexCommand.Install,
                action = () => SelectDoor(d => civilDoor = d)
            };

            yield return new Command_Action
            {
                defaultLabel = "Sélectionner porte spéciale",
                defaultDesc = specialDoor != null ? $"Porte liée en {specialDoor.Position}" : "Aucune porte liée",
                icon = TexCommand.Install,
                action = () => SelectDoor(d => specialDoor = d)
            };
        }

        private void OpenOutfitMenu(System.Action<ApparelPolicy> onPicked)
        {
            var options = new List<FloatMenuOption>();
            foreach (var outfit in Current.Game.outfitDatabase.AllOutfits)
            {
                options.Add(new FloatMenuOption(outfit.label, () => onPicked(outfit)));
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("Aucune politique de tenue existante", null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SelectDoor(System.Action<Building_Door> onPicked)
        {
            var targetParams = new TargetingParameters
            {
                canTargetBuildings = true,
                canTargetPawns = false,
                validator = (TargetInfo t) => t.Thing is Building_Door
            };

            Find.Targeter.BeginTargeting(targetParams, (LocalTargetInfo t) =>
            {
                if (t.Thing is Building_Door door)
                {
                    onPicked(door);
                }
            });
        }

        private void TryForceRedress(Pawn pawn)
        {
            if (pawn.mindState != null)
            {
                pawn.mindState.nextApparelOptimizeTick = 0;
            }

            JobDef currentJobDef = pawn.CurJob?.def;
            bool isExempt = currentJobDef != null && exemptJobs.Contains(currentJobDef);

            if (!isExempt && pawn.jobs != null && pawn.jobs.curJob != null)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }
}