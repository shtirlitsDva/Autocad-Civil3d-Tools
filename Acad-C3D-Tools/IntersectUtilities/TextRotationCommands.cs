using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>KOPIROT, KOPIERROTATION</command>
        /// <summary>
        /// Copies text rotation from a selected MText or MLeader to another MText or MLeader.
        /// </summary>
        /// <category>Text</category>
        [CommandMethod("KOPIROT")]
        [CommandMethod("KOPIERROTATION")]
        public void CopyMTextOrMLeaderRotation()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityResult sourceResult = GetTextRotationEntity(
                        ed,
                        "\nVælg MTEXT/MLEADER der skal kopieres rotation fra: ");
                    if (sourceResult.Status != PromptStatus.OK)
                    {
                        tx.Abort();
                        return;
                    }

                    Entity source = (Entity)tx.GetObject(sourceResult.ObjectId, OpenMode.ForRead);
                    if (!TryGetTextRotation(source, out double rotation, out string sourceError))
                    {
                        ed.WriteMessage($"\n{sourceError}");
                        tx.Abort();
                        return;
                    }

                    PromptEntityResult targetResult = GetTextRotationEntity(
                        ed,
                        "\nVælg MTEXT/MLEADER der skal have samme rotation: ");
                    if (targetResult.Status != PromptStatus.OK)
                    {
                        tx.Abort();
                        return;
                    }

                    Entity target = (Entity)tx.GetObject(targetResult.ObjectId, OpenMode.ForWrite);
                    if (!TrySetTextRotation(target, rotation, out string targetError))
                    {
                        ed.WriteMessage($"\n{targetError}");
                        tx.Abort();
                        return;
                    }

                    tx.Commit();
                    ed.WriteMessage($"\nRotation kopieret: {rotation * 180.0 / System.Math.PI:0.##} grader.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage($"\n{ex.Message}");
                }
            }
        }

        private static PromptEntityResult GetTextRotationEntity(Editor ed, string message)
        {
            PromptEntityOptions options = new(message);
            options.SetRejectMessage("\nVælg kun MTEXT eller MLEADER.");
            options.AddAllowedClass(typeof(MText), false);
            options.AddAllowedClass(typeof(MLeader), false);

            return ed.GetEntity(options);
        }

        private static bool TryGetTextRotation(Entity entity, out double rotation, out string error)
        {
            rotation = 0.0;
            error = string.Empty;

            switch (entity)
            {
                case MText mText:
                    rotation = mText.Rotation;
                    return true;

                case MLeader mLeader:
                    if (!TryGetMLeaderMText(mLeader, out MText leaderText, out error))
                    {
                        return false;
                    }

                    rotation = leaderText.Rotation;
                    return true;

                default:
                    error = "Objektet er ikke MTEXT eller MLEADER.";
                    return false;
            }
        }

        private static bool TrySetTextRotation(Entity entity, double rotation, out string error)
        {
            error = string.Empty;

            switch (entity)
            {
                case MText mText:
                    mText.Rotation = rotation;
                    return true;

                case MLeader mLeader:
                    if (!TryGetMLeaderMText(mLeader, out MText leaderText, out error))
                    {
                        return false;
                    }

                    leaderText.Rotation = rotation;
                    mLeader.MText = leaderText;
                    return true;

                default:
                    error = "Objektet er ikke MTEXT eller MLEADER.";
                    return false;
            }
        }

        private static bool TryGetMLeaderMText(MLeader mLeader, out MText mText, out string error)
        {
            mText = null!;
            error = string.Empty;

            if (mLeader.ContentType != ContentType.MTextContent)
            {
                error = "Den valgte MLEADER har ikke MTEXT-indhold.";
                return false;
            }

            mText = mLeader.MText;
            if (mText == null)
            {
                error = "Kunne ikke læse MTEXT-indhold fra den valgte MLEADER.";
                return false;
            }

            return true;
        }
    }
}
