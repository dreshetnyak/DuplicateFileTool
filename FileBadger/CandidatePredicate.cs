namespace FileBadger
{
    internal interface ICandidatePredicate
    {
        bool IsCandidate(FileData firstFile, FileData secondFile);
    }
}
