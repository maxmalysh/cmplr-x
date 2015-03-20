using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NM_GraphCommon;
using NM_DirectedGraphLibrary.NM_DirectedGraphLibraryInternals;

namespace NM_DirectedGraphLibrary
{

    namespace NM_DirectedGraphLibraryInternals
    {

        public interface IDirectedGraphStorage<
                                        EProperties,
                                        VProperties
                                        > : IGraphStorage<EProperties, VProperties>
        {
            /// <summary>
            /// Позволяет для вершины найти все выходящие дуги.
            /// </summary>
            IEnumerable<CEDescr<VProperties, EProperties>> GetOutEdges(CVDescr<VProperties> v);
        }

        public interface IBiDirectedGraphStorage<
                                        EProperties,
                                        VProperties
                                        > : IDirectedGraphStorage<EProperties, VProperties>
        {
            /// <summary>
            /// Позволяет для вершины найти все входящие дуги.
            /// </summary>
            IEnumerable<CEDescr<VProperties, EProperties>> GetInEdges(CVDescr<VProperties> v);
        }

        /// <summary>
        /// Матричный механизм хранения для направленного графа.
        /// </summary>
        public class CBiDirectedAdjMatrixStorage<
                            EProperties,
                            VProperties
                        > : IBiDirectedGraphStorage<EProperties, VProperties>                          
        {
            #region Private members

            private CEDescr<VProperties, EProperties>[,] edges;
            private CVDescr<VProperties>[] verts;
            private int[] inEdgeCounters;
            private int[] outEdgeCounters;
            private int maxNumVert;
            private int currNumVert = 0;

            private int _allocNum()
            {
                int i = 0;
                while ( i < maxNumVert && verts[i] != null) i++;
                if (i < maxNumVert)
                {
                    currNumVert++;
                    return i;
                }
                else
                    return -1;
            }

            #endregion

            #region Refs

            private class SEdgeRef<VProperties>
            {
                public int beginIdx;
                public int endIdx;

                public SEdgeRef(int beginIdx, int endIdx)
                {
                    this.beginIdx = beginIdx;
                    this.endIdx = endIdx;
                }
            }

            #endregion

            #region Constructors

            public CBiDirectedAdjMatrixStorage(int maxNumVert)
            {
                this.maxNumVert = maxNumVert;
                this.edges = new CEDescr<VProperties, EProperties>[maxNumVert, maxNumVert];
                this.verts = new CVDescr<VProperties>[maxNumVert];
                this.inEdgeCounters = new int[maxNumVert];
                this.outEdgeCounters = new int[maxNumVert];
            }

            #endregion

            public IEnumerable<CVDescr<VProperties>> AllVerts
            {
                get
                {
                    foreach (CVDescr<VProperties> vd in verts)
                    {
                        if (vd != null)
                            yield return vd;
                    }
                }
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> AllEdges
            {
                get
                {
                    for (int beginIdx=0; beginIdx < maxNumVert; beginIdx++)
                    {
                        for (int endIdx=0; endIdx < maxNumVert; endIdx++)
                        {
                            if (edges[beginIdx, endIdx] != null)
                                yield return edges[beginIdx, endIdx];
                        }
                    }                  
                }
            }

            public CVDescr<VProperties> AddVertex(VProperties v)
            {
                int newInt = _allocNum();
                var res = new CVDescr<VProperties>(v, newInt);
                verts[newInt] = res;
                return res;
            }

            public bool DeleteVertex(CVDescr<VProperties> vd)
            {
                Debug.Assert(vd != null && vd.reference != null);
                Debug.Assert(vd.reference is int);

                int idx = (int)(vd.reference);

                Debug.Assert( verts[idx] != null );

                if (inEdgeCounters[idx] != 0)
                    return false;
                if (outEdgeCounters[idx] != 0)
                    return false;

                for (int i = 0; i < maxNumVert; i++)
                    if (edges[i, idx] != null)
                        return false;

                verts[idx] = null;
                vd.reference = null;
                return true;
            }

            public CEDescr<VProperties, EProperties> AddEdge(CVDescr<VProperties> v1, CVDescr<VProperties> v2, EProperties e)
            {
                int beginIdx = (int)(v1.reference);
                int endIdx = (int)(v2.reference);
                SEdgeRef<VProperties> er = new SEdgeRef<VProperties>(beginIdx, endIdx);
                outEdgeCounters[beginIdx]++;
                inEdgeCounters[endIdx]++;
                return edges[beginIdx, endIdx] = new CEDescr<VProperties, EProperties>(e, er, this);
            }

            public bool DeleteEdge(CEDescr<VProperties, EProperties> e)
            {
                Debug.Assert(e != null && e.reference != null);
                Debug.Assert(e.reference is SEdgeRef<VProperties>);

                int beginIdx = (e.reference as SEdgeRef<VProperties>).beginIdx;
                int endIdx = (e.reference as SEdgeRef<VProperties>).endIdx;

                outEdgeCounters[beginIdx]--;
                inEdgeCounters[endIdx]--;

                Debug.Assert(outEdgeCounters[beginIdx] >= 0);
                Debug.Assert(inEdgeCounters[endIdx] >= 0);

                if (edges[beginIdx, endIdx] != null)
                {
                    edges[beginIdx, endIdx] = null;
                    return true;
                }
                else
                    return false;
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetOutEdges(CVDescr<VProperties> v)
            {
                Debug.Assert(v.reference is int);

                int vertexIdx = ((int)v.reference);
 
                for (int currentIdx = 0; currentIdx < maxNumVert; currentIdx++)
                {
                    if (edges[vertexIdx, currentIdx] != null)
                        yield return edges[vertexIdx, currentIdx];
                }
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetInEdges(CVDescr<VProperties> v)
            {
                Debug.Assert(v.reference is int);

                int vertexIdx = ((int)v.reference);
             
                for (int currentIdx = 0; currentIdx < maxNumVert; currentIdx++)
                {
                    if (edges[currentIdx, vertexIdx] != null)
                        yield return edges[currentIdx, vertexIdx];
                }
            }

            public CVDescr<VProperties> GetVBegin(CEDescr<VProperties, EProperties> ed)
            {
                Debug.Assert(ed.reference is SEdgeRef<VProperties>);
                int beginIdx = (ed.reference as SEdgeRef<VProperties>).beginIdx;
                return verts[beginIdx];
            }

            public CVDescr<VProperties> GetVEnd(CEDescr<VProperties, EProperties> ed)
            {
                Debug.Assert(ed.reference is SEdgeRef<VProperties>);
                int endIdx = (ed.reference as SEdgeRef<VProperties>).endIdx;
                return verts[endIdx];
            }
        }

        /// <summary>
        /// списковый механизм хранения графа
        /// </summary>
        /// <typeparam name="EProperties"></typeparam>
        /// <typeparam name="VProperties"></typeparam>
        public class CDirectedAdjListStorage<
                            EProperties,
                            VProperties
                        > : IBiDirectedGraphStorage<EProperties, VProperties>
        {
            private class SRodVertexPair
            {
                public CVDescr<VProperties> vd;
                public LinkedList<CEDescr<VProperties, EProperties>> succs;
                public LinkedList<CEDescr<VProperties, EProperties>> preds;
            }

            private class SSuccVertexPair
            {
                public LinkedListNode<SRodVertexPair> nodeBegin;
                public CVDescr<VProperties> nodeEnd;
                public LinkedListNode<CEDescr<VProperties, EProperties>> listNodeEnd;

                public SSuccVertexPair(
                                        LinkedListNode<SRodVertexPair> nodeBegin,
                                        CVDescr<VProperties> nodeEnd,
                                        LinkedListNode<CEDescr<VProperties, EProperties>> listNodeEnd
                                        )
                {
                    this.nodeBegin = nodeBegin;                
                    this.nodeEnd = nodeEnd;
                    this.listNodeEnd = listNodeEnd;
                }
            }

            private LinkedList<SRodVertexPair> rodVerts = new LinkedList<SRodVertexPair>();
            

            public CDirectedAdjListStorage()
            { }

            public IEnumerable<CVDescr<VProperties>> AllVerts 
            {
                get
                {
                    foreach (SRodVertexPair p in rodVerts)
                    {
                        yield return p.vd;
                    }
                }
            }
          
            public IEnumerable<CEDescr<VProperties, EProperties>> AllEdges 
            {
                get
                {
                    foreach (SRodVertexPair p in rodVerts)
                    {
                        foreach (CEDescr<VProperties, EProperties> ed in p.succs)
                        {
                            yield return ed;
                        }
                    }
                }
            }

            public CVDescr<VProperties> AddVertex(VProperties v)
            {
                SRodVertexPair p = new SRodVertexPair();
                p.vd = new CVDescr<VProperties>(v);
                LinkedListNode<SRodVertexPair> node = rodVerts.AddLast(p);
                p.vd.reference = node;
                p.succs = new LinkedList<CEDescr<VProperties, EProperties>>();
                    
                p.preds = new LinkedList<CEDescr<VProperties, EProperties>>();
                
                return p.vd;
            }

            public bool DeleteVertex(CVDescr<VProperties> vd)
            {
                Debug.Assert(vd.reference is LinkedListNode<SRodVertexPair>);

                LinkedListNode<SRodVertexPair> node = (vd.reference as LinkedListNode<SRodVertexPair>);
                if (node.Value.succs.Count == 0)
                {
                    rodVerts.Remove(node);
                    return true;
                }
                else
                    return false;
            }

            public CEDescr<VProperties, EProperties> AddEdge(CVDescr<VProperties> v1, CVDescr<VProperties> v2, EProperties e)
            {
                Debug.Assert(v1.reference is LinkedListNode<SRodVertexPair>);

                var node1_rod = (v1.reference as LinkedListNode<SRodVertexPair>);
                var ed = new CEDescr<VProperties, EProperties>(e, null, this);
                LinkedListNode<CEDescr<VProperties, EProperties>> node2 = node1_rod.Value.succs.AddLast(ed);
                ed.reference = new SSuccVertexPair(node1_rod, v2, node2);

                Debug.Assert(v2.reference is LinkedListNode<SRodVertexPair>);

                var node2_rod = (v2.reference as LinkedListNode<SRodVertexPair>);
                node2_rod.Value.preds.AddLast(ed);
                

                return ed;
            }

            public bool DeleteEdge(CEDescr<VProperties, EProperties> e)
            {
                Debug.Assert(e.reference is SSuccVertexPair);
                Debug.Assert(e.VBegin.reference is LinkedListNode<SRodVertexPair>);

                var itemToBeRemoved = (e.reference as SSuccVertexPair).listNodeEnd;
                var edgeList = (e.VBegin.reference as LinkedListNode<SRodVertexPair>).Value.succs;
                edgeList.Remove(itemToBeRemoved);

                
                return true;
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetOutEdges(CVDescr<VProperties> v)
            {
                Debug.Assert(v.reference is LinkedListNode<SRodVertexPair>);

                var succs = (v.reference as LinkedListNode<SRodVertexPair>).Value.succs;
                foreach (CEDescr<VProperties, EProperties> ed in succs)
                {
                    yield return ed;
                }
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetInEdges(CVDescr<VProperties> v)
            {
                return null;
            }

            public CVDescr<VProperties> GetVBegin(CEDescr<VProperties, EProperties> ed)
            {
                Debug.Assert(ed.reference is SSuccVertexPair);

                return (ed.reference as SSuccVertexPair).nodeBegin.Value.vd;
            }

            public CVDescr<VProperties> GetVEnd(CEDescr<VProperties, EProperties> ed)
            {
                Debug.Assert(ed.reference is SSuccVertexPair);

                return (ed.reference as SSuccVertexPair).nodeEnd;
            }

        }

        public abstract class CDirGraph<
                            EProperties,
                            VProperties>
        {
            private IBiDirectedGraphStorage<EProperties, VProperties> storage;

            public CDirGraph(IBiDirectedGraphStorage<EProperties, VProperties> storage)
            {
                this.storage = storage;
            }

            public IEnumerable<CVDescr<VProperties>> AllVerts
            {
                get 
                {
                    return this.storage.AllVerts;
                }
            }
         
            public IEnumerable<CEDescr<VProperties, EProperties>> AllEdges 
            {
                get
                {
                    return this.storage.AllEdges;
                }
            }

            public CVDescr<VProperties> AddVertex(VProperties v)
            {
                return this.storage.AddVertex(v);
            }

            public bool DeleteVertex(CVDescr<VProperties> vd)
            {
                return this.storage.DeleteVertex(vd);
            }

            public CEDescr<VProperties, EProperties> AddEdge(CVDescr<VProperties> v1, CVDescr<VProperties> v2, EProperties e)
            {
                return this.storage.AddEdge(v1, v2, e);
            }

            public bool DeleteEdge(CEDescr<VProperties, EProperties> e)
            {
                return this.storage.DeleteEdge(e);
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetOutEdges(CVDescr<VProperties> v)
            {
                return this.storage.GetOutEdges(v);
            }

            public IEnumerable<CEDescr<VProperties, EProperties>> GetInEdges(CVDescr<VProperties> v)
            {
                return this.storage.GetInEdges(v);
            }
        }

    }

    public static class CGraphVisualization
    {
        public static void EmitGraphVizFormat<EProperties, VProperties>(
                this NM_DirectedGraphLibraryInternals.CDirGraph<EProperties, VProperties> g)
        {
            Console.WriteLine("@ vertexes:");
            foreach (CVDescr<VProperties> vd in g.AllVerts)
            {
                Console.WriteLine(vd.props.ToString());
            }

            Console.WriteLine("@ edges:");
            foreach (CEDescr<VProperties, EProperties> ed in g.AllEdges)
            {
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("\t\tBegin vertex= '" + ed.VBegin.props.ToString() + "'");
                Console.WriteLine("\t\tEnd vertex= '" + ed.VEnd.props.ToString() + "'");
                Console.WriteLine("\t\tEProperties = '" + ed.props.ToString() + "'");
            }

        }
    }

}