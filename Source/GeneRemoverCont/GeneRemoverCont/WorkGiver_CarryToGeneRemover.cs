using RimWorld;
using Verse;


namespace GeneRemoverCont
{
    public class WorkGiver_CarryToGeneRemover : WorkGiver_CarryToBuilding
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GeneRemover_DefOfs.GeneRemover);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.BiotechActive;
        }
    }
}
