# Introduction #

Здесь дано описание **NS\_GraphCommon/NS\_DirectedGraphLibrary**

# Details #

**GraphCommon.cs** - классы/интерфейсы, которые относятся как для ориентированных, так и для неориентированных графов

**`public class CVDescr<VProperties>`** - описатель вершины. Содержит в себе сведения (поле reference) о том, где эта вершина в графе располагается, а также св-ва вершины (VProperties). Например, для CFG VProperties - список инструкций.

**`public class CEDescr<VProperties, EProperties>`** - описатель ребра. Содержит в себе сведения о том (поле reference), где это ребро в графе располагается, а также св-ва ребра (EProperties). Например, для CFG EProperties - метка перехода.

**`public interface IGraphStorage<EProperties, VProperties>`** - базовый интерфейс для классов, которые будут реализовывать механизм хранения графа. Например, списки или матрица. Пара функций GetVBegin `/`GetVEnd возвращают начальную и конечную вершину ребра.

**`DirectedGraph.cs`** - реализация направленного графа.

**`public interface IDirectedGraphStorage<EProperties, VProperties, GProperties> : IGraphStorage<EProperties, VProperties>`** - stоrage для направленного графа. Все функции за исключением GetOutEdges можно перенести в IGraphStorage. Метод GetOutEdges позволяет для вершины найти все выходящие дуги.

**{{{public interface IBiDirectedGraphStorage<
> EProperties,
> VProperties,
> GProperties> :
> > IDirectedGraphStorage<
> > > EProperties, VProperties, GProperties>}}}** - stоrage для направленного графа. Добавлен дополнительный метод GetOutEdges. Этот метод позволяет для вершины найти все входящие дуги.

**{{{

> public class CBiDirectedAdjMatrixStorage<
> > EProperties,
> > VProperties,
> > GProperties> :
> > > IBiDirectedGraphStorage<
> > > > EProperties, VProperties, GProperties>
}}}** - матричный механизм хранения для направленного графа.

**{{{

> public class CDirectedAdjListStorage<
> > EProperties,
> > VProperties,
> > GProperties> :
> > > IDirectedGraphStorage<
> > > > EProperties, VProperties, GProperties>
}}}** - cписковый механизм хранения для направленного графа.

public abstract class CDirGraph<

> EProperties,
> VProperties,
> GProperties>
Направленный граф. Член storage - объект, который реализует как минимум IDirectedGraphStorage. В этом классе должен быть
> сконцентрирован функционал, который связан с направленными графами DFS-обход, BFS-обход, топологическая сортировка, поиск
> доминаторов и т.д.


public class CDirAdjMtrxGraph<
> EProperties,
> VProperties,
> GProperties> : NM\_DirectedGraphLibraryInternals.CDirGraph<EProperties, VProperties, GProperties>
Направленный граф с матричным механизмом хранения.

public class CDirAdjListGraph<
> EProperties,
> VProperties,
> GProperties> : NM\_DirectedGraphLibraryInternals.CDirGraph<EProperties, VProperties, GProperties>
Направленный граф со списковым механизмом хранения.