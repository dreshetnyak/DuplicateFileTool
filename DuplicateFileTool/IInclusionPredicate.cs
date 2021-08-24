namespace DuplicateFileTool
{
    internal interface IInclusionPredicate<in T>
    {
        bool IsIncluded(T item);
    }
}
