using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiIndexBuilder
{
    public class TermObject
    {
        public byte SectionFlags { get; set; }
        public int TermFrequency { get; set; }

        public List<int> positionList {  get; set; }
        public TermObject(byte sectionFlags = 0, int frequency = 0)
        {
            SectionFlags = sectionFlags;
            TermFrequency = frequency;
            positionList = new List<int>();
        }
        public override string ToString()
        {
            string initialString = $"{SectionFlags}${TermFrequency}";
            if (positionList.Count > 0) initialString += '|';
            for (int i = 0;i<positionList.Count;i++)
            {
                initialString += positionList[i].ToString() ;
                if(i < positionList.Count-1)initialString += '-';
            }
            return initialString;
        }
    }
}
