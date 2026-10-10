#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
#endif

namespace IntersectUtilities.UtilsCommon
{
    /// <summary>
    /// Inserts references of one block into model space, with its attributes. Everything an
    /// insert needs that does not change from one to the next (model space open for write,
    /// the block's record, its attribute definitions) is looked up once, when it is made:
    /// inserting thousands costs one lookup, not thousands.
    /// Works inside the database's top transaction and is valid only while that is open.
    /// <see cref="Extensions.CreateBlockWithAttributes"/> is one insert through it.
    /// </summary>
    public sealed class BlockInserter
    {
        private readonly Transaction m_tx;
        private readonly BlockTableRecord m_modelSpace;
        private readonly ObjectId m_block;
        private readonly IReadOnlyList<AttributeDefinition> m_attributes;

        /// <summary>The block must exist in the database: check or import it first.</summary>
        public BlockInserter(Database db, string blockName)
        {
            m_tx = db.TransactionManager.TopTransaction;
            m_modelSpace = db.GetModelspaceForWrite();
            var blockTable = (BlockTable)m_tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            m_block = blockTable[blockName];
            var record = (BlockTableRecord)m_tx.GetObject(m_block, OpenMode.ForRead);
            var attributes = new List<AttributeDefinition>();
            foreach (ObjectId id in record)
                if (id.IsDerivedFrom<AttributeDefinition>())
                {
                    var definition = (AttributeDefinition)m_tx.GetObject(id, OpenMode.ForRead);
                    if (!definition.Constant)
                        attributes.Add(definition);
                }
            m_attributes = attributes;
        }

        /// <param name="position">Where the block is inserted.</param>
        /// <param name="rotation">The block's rotation, radians.</param>
        /// <param name="scale">The block's uniform scale, set before its attributes are placed.</param>
        public BlockReference Insert(Point3d position, double rotation = 0, double scale = 1.0)
        {
            var br = new BlockReference(position, m_block);
            br.ScaleFactors = new Scale3d(scale);
            m_modelSpace.AppendEntity(br);
            m_tx.AddNewlyCreatedDBObject(br, true);
            br.Rotation = rotation;

            foreach (AttributeDefinition definition in m_attributes)
            {
                using (AttributeReference attribute = new AttributeReference())
                {
                    attribute.SetAttributeFromBlock(definition, br.BlockTransform);
                    attribute.Position = definition.Position.TransformBy(br.BlockTransform);
                    attribute.TextString = definition.getTextWithFieldCodes();
                    br.AttributeCollection.AppendAttribute(attribute);
                    m_tx.AddNewlyCreatedDBObject(attribute, true);
                }
            }
            return br;
        }
    }
}
