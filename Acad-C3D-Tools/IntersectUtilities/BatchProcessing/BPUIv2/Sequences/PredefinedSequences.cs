using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public static class PredefinedSequences
{
    public static IReadOnlyList<SequenceDefinition> GetAll()
    {
        return new List<SequenceDefinition>
        {
            // #1 fixler2dplotstyles
            new("Fix Ler2D Plot Styles",
                "Sets all layers from the specified Ler 2D xref to plot style Nedtonet 50%.",
                "Layer Operations",
                SequenceStorageLevel.Predefined,
                Step("Layer.SetPlotStyle",
                    P("xrefName", ""),
                    P("plotStyleName", "Nedtonet 50%"))),

            // #2 fixdrawings
            new("Fix Profile Styles",
                "Sets all profile styles to DRI standard and configures profile views.",
                "Profile Operations",
                SequenceStorageLevel.Predefined,
                Step("Profile.SetStyleProperties",
                    P("styleName", "PROFIL STYLE MGO MIDT"),
                    P("linetypeScale", 10.0),
                    P("lineWeight", 0)),
                Step("Profile.SetStyle",
                    P("namePattern", "TOP|BUND"),
                    P("styleName", "PROFIL STYLE MGO KANT")),
                Step("Profile.SetStyle",
                    P("namePattern", "MIDT"),
                    P("styleName", "PROFIL STYLE MGO MIDT")),
                Step("Profile.CreateCurveLabels",
                    P("namePattern", "MIDT"),
                    P("crestStyleName", "Radius Crest"),
                    P("sagStyleName", "Radius Sag")),
                Step("Profile.SetViewBandItems",
                    P("bandIndex", 0),
                    P("surfaceProfileSuffix", "_surface_P"),
                    P("topProfilePattern", "TOP"),
                    P("labelAtStart", true),
                    P("labelAtEnd", true))),

            // #3 fixnewlabelstyles
            new("Import & Stagger Label Styles",
                "Imports civil styles and staggers all labels.",
                "Style Operations",
                SequenceStorageLevel.Predefined,
                Step("Style.ImportCivilStyles"),
                Step("Style.StaggerLabels")),

            // #4 attachdwg
            new("Attach Xref with Draw Order",
                "Attaches a .dwg as xref at origin and optionally sets draw order.",
                "Xref Operations",
                SequenceStorageLevel.Predefined,
                Step("Xref.Attach",
                    P("filePath", ""),
                    P("layerName", "")),
                Step("Xref.SetDrawOrder",
                    P("entityXrefName", ""),
                    P("referenceXrefName", ""),
                    P("orderType", "Under"))),

            // #5 detachdwg
            new("Detach Xref",
                "Detaches an xref by name, outputting its path and layer.",
                "Xref Operations",
                SequenceStorageLevel.Predefined,
                Step("Xref.Detach",
                    P("xrefName", ""))),

            // #6 detachattachdwg — uses bindings for dataflow
            DetachReattachSequence(),

            // #7 createdetailing
            new("Recreate Detailing",
                "Deletes existing detailing and creates new longitudinal profile detailing.",
                "Detailing Operations",
                SequenceStorageLevel.Predefined,
                Step("Detailing.Delete"),
                Step("Detailing.Create",
                    PDro())),

            // #8 deletedetailing
            new("Delete Detailing",
                "Deletes existing longitudinal profile detailing.",
                "Detailing Operations",
                SequenceStorageLevel.Predefined,
                Step("Detailing.Delete")),

            // #9 deleteallcogopoints
            new("Delete Cogo Points",
                "Deletes all Cogo Points and erases blocks at profile view locations.",
                "Detailing Operations",
                SequenceStorageLevel.Predefined,
                Step("Detailing.DeleteCogoPoints"),
                Step("Block.EraseAtProfileViews")),

            // #10 createreferencetopipeprofiles
            new("Create Pipe Profile References",
                "Creates pipe profile references from data shortcuts.",
                "DataShortcut Operations",
                SequenceStorageLevel.Predefined,
                Step("DataShortcut.CreatePipeProfileRefs")),

            // #11 createreferencetosurfaceprofile
            new("Create Surface Profile Reference",
                "Creates surface profile reference from data shortcuts.",
                "DataShortcut Operations",
                SequenceStorageLevel.Predefined,
                Step("DataShortcut.CreateSurfaceProfileRef")),

            // #12 fixlongitudinalprofiles
            new("Fix Longitudinal Profiles",
                "Auto-calculates elevation range, sets profile view style, terrain profile style, and creates preliminary detailing.",
                "Profile Operations",
                SequenceStorageLevel.Predefined,
                Step("Profile.SetViewElevationRange",
                    P("minDepth", 3.0)),
                Step("Profile.SetViewStyle",
                    P("styleName", "PROFILE VIEW L TO R 1:250:100")),
                Step("Layer.CreateOrCheck",
                    P("layerName", "0_TERRAIN_PROFILE"),
                    P("colorIndex", 34)),
                Step("Profile.SetSurfaceProfileStyle",
                    P("profileStyleName", "Terræn")),
                Step("Detailing.CreatePreliminary",
                    PDro())),

            // #13 listvfnumbers
            new("List ViewFrame Numbers",
                "Lists all ViewFrame numbers and warns if they don't follow sequence.",
                "ViewFrame Operations",
                SequenceStorageLevel.Predefined,
                Step("ViewFrame.ListNumbers",
                    PCounter())),

            // #14 renumbervfs
            new("Renumber ViewFrames",
                "Renumbers all ViewFrames sequentially across drawings.",
                "ViewFrame Operations",
                SequenceStorageLevel.Predefined,
                Step("ViewFrame.Renumber",
                    PCounter())),

            // #15 correctfieldinblock
            new("Correct Tegningsskilt Field",
                "Corrects the SAG2 field reference in the Tegningsskilt block.",
                "Block Operations",
                SequenceStorageLevel.Predefined,
                Step("Block.CorrectField",
                    P("blockName", "Tegningsskilt"),
                    P("attributeTag", "SAG2"),
                    P("fieldCode", "%<\\AcSm SheetSet.Description \\f \"%tc1\">%"))),

            // #16 hidealignments
            new("Hide Alignments (HAL)",
                "Sets alignment style to NO SHOW and imports _No Labels label set.",
                "Alignment Operations",
                SequenceStorageLevel.Predefined,
                Step("Alignment.SetStyle",
                    P("styleName", "FJV TRACE NO SHOW")),
                Step("Alignment.ImportLabelSet",
                    P("labelSetName", "_No Labels"))),

            // #17 alignmentsnoshow
            new("Alignment NO SHOW",
                "Sets alignment style to NO SHOW without changing labels.",
                "Alignment Operations",
                SequenceStorageLevel.Predefined,
                Step("Alignment.SetStyle",
                    P("styleName", "FJV TRACE NO SHOW"))),

            // #18 alignmentsnoshowandlabels
            new("Alignment NO SHOW + Labels",
                "Sets alignment style to NO SHOW and imports STD 20-5 label set.",
                "Alignment Operations",
                SequenceStorageLevel.Predefined,
                Step("Alignment.SetStyle",
                    P("styleName", "FJV TRACE NO SHOW")),
                Step("Alignment.ImportLabelSet",
                    P("labelSetName", "STD 20-5"))),

            // #19 centerviewframes
            new("Center ViewFrame Labels",
                "Centers the viewframe label text component (MiddleCenter, zero offset).",
                "Alignment Operations",
                SequenceStorageLevel.Predefined,
                Step("Alignment.CenterViewFrameLabels")),

            // #20 vpfreezelayers
            new("Freeze LER Points in Minimap",
                "Finds all ProfileProjectionLabel layers and freezes them in the minimap viewport.",
                "Viewport Operations",
                SequenceStorageLevel.Predefined,
                Step("Viewport.FreezeLERPointLayers",
                    P("viewportCenterX", 0),
                    P("viewportCenterY", 0))),

            // #21 vpfreezecannomtch
            new("Freeze Match Line in Minimap",
                "Freezes C-ANNO-MTCH layers in the minimap viewport.",
                "Viewport Operations",
                SequenceStorageLevel.Predefined,
                Step("Viewport.FreezeMatchLine",
                    P("viewportCenterX", 0),
                    P("viewportCenterY", 0))),

            // #22 vpstylizelayers
            new("Stylize Minimap Layers",
                "Freezes xref layers in minimap viewport and sets color override on Bygning/Vejkant layers.",
                "Viewport Operations",
                SequenceStorageLevel.Predefined,
                Step("Viewport.FreezeXrefLayers",
                    P("xrefName", ""),
                    P("viewportCenterX", 0),
                    P("viewportCenterY", 0)),
                Step("Viewport.SetLayerColor",
                    P("xrefName", ""),
                    P("layerPatterns", "Bygning;Vejkant"),
                    P("colorName", "grey"),
                    P("viewportCenterX", 0),
                    P("viewportCenterY", 0))),

            // #23 placeblockpaperspace
            new("Place Block on Paperspace",
                "Places a block on paperspace with rotation from viewport twist angle.",
                "Block Operations",
                SequenceStorageLevel.Predefined,
                Step("Block.PlaceOnPaperspace",
                    P("blockName", ""),
                    P("blockX", 0),
                    P("blockY", 0),
                    P("viewportCenterX", 0),
                    P("viewportCenterY", 0))),

            // #24 replaceblockpaperspace
            new("Replace Block on Paperspace",
                "Deletes old block, imports new from library, and places on all layouts.",
                "Block Operations",
                SequenceStorageLevel.Predefined,
                Step("Block.ReplaceOnPaperspace",
                    P("oldBlockName", ""),
                    P("libraryPath", ""),
                    P("newBlockName", ""),
                    P("blockX", 0),
                    P("blockY", 0))),

            // #25 unhidelayer
            new("Unhide Layers",
                "Unfreezes and turns on specified layers (semicolon-delimited).",
                "Layer Operations",
                SequenceStorageLevel.Predefined,
                Step("Layer.SetVisibility",
                    P("layerNames", ""),
                    P("xrefName", ""),
                    P("frozen", false),
                    P("off", false))),

            // #26 hidelayer
            new("Hide Layers",
                "Freezes and turns off specified layers (semicolon-delimited).",
                "Layer Operations",
                SequenceStorageLevel.Predefined,
                Step("Layer.SetVisibility",
                    P("layerNames", ""),
                    P("xrefName", ""),
                    P("frozen", true),
                    P("off", true))),

            // #27 unfreezelayer
            new("Unfreeze Layers",
                "Unfreezes specified layers, with optional xref prefix.",
                "Layer Operations",
                SequenceStorageLevel.Predefined,
                Step("Layer.SetVisibility",
                    P("layerNames", ""),
                    P("xrefName", ""),
                    P("frozen", false),
                    P("off", false))),

            // #28 changelayerlinetype
            new("Change Layer Linetype",
                "Changes the linetype of specified layers.",
                "Layer Operations",
                SequenceStorageLevel.Predefined,
                Step("Layer.SetLinetype",
                    P("layerNames", ""),
                    P("linetypeName", ""))),

            // #29 changelayerforxref
            new("Change Xref Layer",
                "Changes the layer of an xref matched by partial name.",
                "Xref Operations",
                SequenceStorageLevel.Predefined,
                Step("Xref.ChangeLayer",
                    P("xrefPartialName", ""),
                    P("layerName", ""))),

            // #30 deletewronglyshortcuttedprofilesandalignments
            new("Delete Wrong Shortcuts",
                "Deletes all profiles and erases alignments whose name contains '('.",
                "Profile Operations",
                SequenceStorageLevel.Predefined,
                Step("Profile.EraseAll"),
                Step("Profile.EraseAlignmentsByPattern",
                    P("pattern", "("))),
        };
    }

    private static SequenceDefinition DetachReattachSequence()
    {
        const string detachId = "detach01";
        var def = new SequenceDefinition(
            "Detach & Reattach Xref",
            "Detaches an xref (capturing path+layer via outputs), then reattaches it with optional draw order.",
            "Xref Operations",
            SequenceStorageLevel.Predefined,
            StepWithId(detachId, "Xref.Detach",
                P("xrefName", "")),
            StepWithId("attach01", "Xref.Attach",
                PBound("filePath", ParameterType.String, detachId, "xrefPath"),
                PBound("layerName", ParameterType.String, detachId, "xrefLayer")),
            Step("Xref.SetDrawOrder",
                P("entityXrefName", ""),
                P("referenceXrefName", ""),
                P("orderType", "Under")));
        return def;
    }

    #region Helper methods

    private static OperationStep Step(string typeId, params (string key, ParameterValue val)[] parameters)
    {
        var dict = new Dictionary<string, ParameterValue>();
        foreach (var (key, val) in parameters)
            dict[key] = val;
        return new OperationStep(typeId, dict);
    }

    private static OperationStep StepWithId(string stepId, string typeId,
        params (string key, ParameterValue val)[] parameters)
    {
        var step = Step(typeId, parameters);
        step.StepId = stepId;
        return step;
    }

    private static (string, ParameterValue) P(string name, string value)
        => (name, new ParameterValue { Type = ParameterType.String, Value = value });

    private static (string, ParameterValue) P(string name, int value)
        => (name, new ParameterValue { Type = ParameterType.Int, Value = value });

    private static (string, ParameterValue) P(string name, double value)
        => (name, new ParameterValue { Type = ParameterType.Double, Value = value });

    private static (string, ParameterValue) P(string name, bool value)
        => (name, new ParameterValue { Type = ParameterType.Bool, Value = value });

    private static (string, ParameterValue) PBound(string name, ParameterType type,
        string sourceStepId, string outputName)
        => (name, new ParameterValue
        {
            Type = type,
            Binding = new OutputBinding { SourceStepId = sourceStepId, OutputName = outputName }
        });

    private static (string, ParameterValue) PDro()
        => ("dro", new ParameterValue { Type = ParameterType.DataReferencesOptions });

    private static (string, ParameterValue) PCounter()
        => ("counter", new ParameterValue { Type = ParameterType.Counter });

    #endregion
}
