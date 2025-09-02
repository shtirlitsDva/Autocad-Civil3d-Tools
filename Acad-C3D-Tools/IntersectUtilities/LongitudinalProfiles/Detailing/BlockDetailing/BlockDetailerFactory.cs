using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    public sealed class BlockDetailerFactory
    {
        public static IBlockDetailer? Resolve(PipelineElementType type, bool preliminary = false) => type switch
        {
            PipelineElementType.Pipe => null,

            //Afgreninger
            PipelineElementType.AfgreningParallel
            or PipelineElementType.LigeAfgrening 
            or PipelineElementType.AfgreningMedSpring 
            or PipelineElementType.Svejsetee 
            or PipelineElementType.PreskoblingTee 
            or PipelineElementType.Stikafgrening 
            or PipelineElementType.Muffetee 
            => new DetailerAfgrening(),

            //Afgreninger med inverteret tilhørsforhold
            PipelineElementType.Afgreningsstuds
            or PipelineElementType.Svanehals 
            => new DetailerAfgreningBelongsToInverted(),

            //Generic
            PipelineElementType.Endebund or
            PipelineElementType.Engangsventil or
            PipelineElementType.F_Model or
            PipelineElementType.Y_Model or
            PipelineElementType.Kedelrørsbøjning or
            PipelineElementType.PræisoleretBøjning90gr or
            PipelineElementType.Bøjning45gr or
            PipelineElementType.Bøjning30gr or
            PipelineElementType.Bøjning15gr or
            PipelineElementType.PræisoleretBøjningVariabel or
            PipelineElementType.PræisoleretVentil or
            PipelineElementType.PræventilMedUdluftning            
            => new DetailerGeneric(),

            //Buerør
            PipelineElementType.Buerør => new DetailerBuerør(),

            //Detailed elsewhere
            PipelineElementType.Svejsning when preliminary => null,
            PipelineElementType.Svejsning => null, //right now handled else where
            PipelineElementType.Reduktion => null,
            PipelineElementType.Materialeskift => null,

            _ => throw new System.NotImplementedException($"{type} not implemented! #ERR:2456752"),
        };
    }
}
