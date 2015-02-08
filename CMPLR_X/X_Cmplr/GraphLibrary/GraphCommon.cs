using System;
using System.Reflection;
using System.Collections.Generic;

namespace NM_GraphCommon
{
    /// <summary>
    /// Описатель вершины. (поле reference)   -   где эта вершина в графе располагается.
    /// </summary>
    /// <typeparam name="VProperties">Св-ва вершины. Например, для CFG VProperties  -  список инструкций.</typeparam>
    public class CVDescr<VProperties>
    {
        public VProperties props;
        public object reference;

        public CVDescr(VProperties props, object reference = null)
        {
            this.props = props;
            this.reference = reference;
            System.Type type = typeof(VProperties);
            PropertyInfo pi = type.GetProperty("VDescr");
            pi.SetValue(props, this);
        }
    }

    /// <summary>
    /// Описатель ребра. (поле reference)   -   где это ребро в графе располагается.
    /// </summary>
    /// <typeparam name="EProperties">Св-ва ребра. Например, для CFG EProperties  -  метка перехода.</typeparam>
    /// <typeparam name="VProperties">Св-ва вершины. Например, для CFG VProperties  -  список инструкций.</typeparam>
    public partial class CEDescr<VProperties, EProperties>
    {
        public EProperties props;
        public object reference;
        public IGraphStorage<EProperties, VProperties> storage;

        public CEDescr(EProperties props, object reference, IGraphStorage<EProperties, VProperties> storage)
        {
            this.props = props;
            this.reference = reference;
            this.storage = storage;
        }

        public CVDescr<VProperties> VBegin
        {
            get
            {
                return storage.GetVBegin(this);
            }
        }

        public CVDescr<VProperties> VEnd
        {
            get
            {
                return storage.GetVEnd(this);
            }
        }
    }


    /// <summary>
    /// Базовый интерфейс для классов, которые будут реализовывать механизм хранения графа. 
    /// Например, списки или матрица. 
    /// </summary>
    /// <typeparam name="EProperties">Св-ва ребра. Например, для CFG EProperties  -  метка перехода.</typeparam>
    /// <typeparam name="VProperties">Св-ва вершины. Например, для CFG VProperties  -  список инструкций.</typeparam>
    public interface IGraphStorage<EProperties, VProperties>
    {
        /// <summary>
        /// Возвращают начальную вершину ребра ed.
        /// </summary>
        CVDescr<VProperties> GetVBegin(CEDescr<VProperties, EProperties> ed);
        /// <summary>
        /// Возвращают конечную вершину ребра ed.
        /// </summary>
        CVDescr<VProperties> GetVEnd(CEDescr<VProperties, EProperties> ed);

        /// <summary>
        /// access to whole vertex collection
        /// </summary>
        IEnumerable<CVDescr<VProperties>> AllVerts { get; }

        /// <summary>
        /// access to whole edge collection
        /// </summary>
        IEnumerable<CEDescr<VProperties, EProperties>> AllEdges { get; }

        // add/delete vertex 
        CVDescr<VProperties> AddVertex(VProperties v);
        bool DeleteVertex(CVDescr<VProperties> vd);

        // add/delete edges
        CEDescr<VProperties, EProperties> AddEdge(CVDescr<VProperties> v1, CVDescr<VProperties> v2, EProperties e);
        bool DeleteEdge(CEDescr<VProperties, EProperties> e);
    }
}