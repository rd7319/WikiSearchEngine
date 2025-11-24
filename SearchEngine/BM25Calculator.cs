using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public static class BM25
    {
        // Standard parameter tuning values
        private const double k1 = 1.5;
        private const double b = 0.75;

        /// <summary>
        /// Computes BM25 score for a single term in a document.
        /// </summary>
        /// <param name="termFreq">Frequency of term in document (f(t, d))</param>
        /// <param name="docFreq">Number of documents containing the term (n_t)</param>
        /// <param name="docLength">Number of words in this document (|d|)</param>
        /// <param name="avgDocLength">Average document length (avgdl)</param>
        /// <param name="totalDocs">Total number of documents (N)</param>
        /// <returns>BM25 score contribution for this term</returns>
        public static double ComputeBM25Score(
            int termFreq,
            int docFreq,
            int docLength,
            double avgDocLength,
            int totalDocs)
        {
            if (termFreq <= 0 || docFreq == 0 || docLength == 0)
                return 0.0;

            // Compute IDF component
            double idf = Math.Log((totalDocs - docFreq + 0.5) / (docFreq + 0.5) + 1);

            // TF normalization component
            double numerator = termFreq * (k1 + 1);
            double denominator = termFreq + k1 * (1 - b + b * (docLength / avgDocLength));

            return idf * (numerator / denominator);
        }
    }
}
