using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Vestibule
{
    public class RoomRoleWorker_Vestibule : RoomRoleWorker
    {
        public override float GetScore(Room room)
        {
            List<Thing> things = room.ContainedAndAdjacentThings;

            bool hasLocker = things.Any(t => t.TryGetComp<CompEntryWardrobe>() != null);
            int doorCount = things.OfType<Building_Door>().Distinct().Count();

            if (hasLocker && doorCount >= 2)
            {
                return 100000f;
            }
            return 0f;
        }
    }
}
