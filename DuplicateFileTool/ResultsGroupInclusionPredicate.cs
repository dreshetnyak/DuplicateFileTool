using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuplicateFileTool
{
    internal class ResultsGroupInclusionPredicate : IInclusionPredicate<DuplicateGroup>
    {
        public bool IsIncluded(DuplicateGroup item)
        {
            //TODO
            return true;
        }
    }
}
