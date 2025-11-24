using System;
using System.Text;

namespace WikiIndexBuilder
{
    /// <summary>
    /// Implements the classic Porter Stemming Algorithm (1980).
    /// </summary>
    public class PorterStemmer
    {
        // Internal char array to hold the word being stemmed.
        private char[] _word;
        // The index of the last character of the word in _word.
        private int _k;
        // The index of the end of the stem (the part that remains after stemming).
        private int _k0;
        // The index of the first character of the suffix (the part to be removed or replaced).
        private int _j;

        // Constants for character checking.
        private const char Consonant = 'c';
        private const char Vowel = 'v';

        /// <summary>
        /// Stems a single word according to the Porter algorithm.
        /// </summary>
        /// <param name="s">The word to stem (must be lowercase).</param>
        /// <returns>The stemmed word.</returns>
        public string Stem(string s)
        {
            // Only process if the word has more than 2 letters.
            if (s.Length < 3)
            {
                return s;
            }

            _word = s.ToCharArray();
            _k = s.Length - 1;
            _k0 = 0; // Start index

            // Check for initial 'y'
            if (_word[_k0] == 'y') _word[_k0] = 'Y';

            Step1a();
            //Step1b();
            Step1c();
            Step2();
            Step3();
            Step4();
            Step5a();
            Step5b();

            // Revert 'Y' back to 'y'
            for (int i = 0; i <= _j; i++)
            {
                if (_word[i] == 'Y')
                {
                    _word[i] = 'y';
                }
            }

            return new string(_word, 0, _j + 1);
        }

        // === Helper Methods ===

        /// <summary>
        /// Checks if a character at index i is a consonant.
        /// </summary>
        private bool IsConsonant(int i)
        {
            char c = _word[i];

            if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == 'y' || c == 'Y')
            {
                return false;
            }
            if (i > _k0 && c == 'y' && !IsConsonant(i - 1))
            {
                return false; // 'y' preceded by a vowel is considered a vowel
            }
            return true;
        }

        /// <summary>
        /// Measures the sequence of CVC (consonant-vowel-consonant) patterns in the stem.
        /// m is the number of (VC) sequences.
        /// </summary>
        private int GetMeasure()
        {
            int n = 0;
            int i = _k0;

            // Skip initial consonants
            while (true)
            {
                if (i > _j) return n;
                if (!IsConsonant(i)) break;
                i++;
            }
            i++;

            while (true)
            {
                // Find next consonant
                while (true)
                {
                    if (i > _j) return n;
                    if (IsConsonant(i)) break;
                    i++;
                }
                i++;
                n++; // Found one (VC) sequence

                // Skip vowels
                while (true)
                {
                    if (i > _j) return n;
                    if (!IsConsonant(i)) break;
                    i++;
                }
                i++;
            }
        }

        /// <summary>
        /// Checks if the stem contains a vowel.
        /// </summary>
        private bool ContainsVowel()
        {
            for (int i = _k0; i <= _j; i++)
            {
                if (!IsConsonant(i))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the stem ends in a double consonant, e.g., 'bb', 'dd'.
        /// </summary>
        private bool EndsInDoubleConsonant(int j)
        {
            if (j < _k0 + 1) return false;
            if (_word[j] != _word[j - 1]) return false;
            return IsConsonant(j);
        }

        /// <summary>
        /// Checks for special CVC case: consonant-vowel-consonant, 
        /// where the final C is not W, X, or Y (e.g., 'wil', 'hop').
        /// </summary>
        private bool CvC(int i)
        {
            if (i < _k0 + 2 || !IsConsonant(i) || IsConsonant(i - 1) || !IsConsonant(i - 2))
                return false;

            char c = _word[i];
            return c != 'w' && c != 'x' && c != 'Y'; // 'Y' is used temporarily for 'y'
        }

        /// <summary>
        /// Checks if the word ends with a given suffix. Sets the new stem length (_j) if true.
        /// </summary>
        /// <param name="s">The suffix string.</param>
        /// <returns>True if the word ends with the suffix.</returns>
        private bool EndsWith(string s)
        {
            int l = s.Length;
            int offset = _k - l + 1;

            if (offset < _k0) return false;

            for (int i = 0; i < l; i++)
            {
                if (_word[offset + i] != s[i]) return false;
            }

            // If it ends with the suffix, update the new end of the stem.
            _j = _k - l;
            return true;
        }

        /// <summary>
        /// Replaces the current suffix (if any) with a replacement string, 
        /// provided the resulting stem has a measure (m) greater than 'minM'.
        /// </summary>
        private bool Replace(string s, int minM)
        {
            if (GetMeasure() > minM)
            {
                int l = s.Length;

                // Copy replacement string into the word array
                for (int i = 0; i < l; i++)
                {
                    _word[_j + 1 + i] = s[i];
                }

                _k = _j + l; // Update word end index
                return true;
            }
            return false;
        }

        // === Stemming Steps ===

        // Step 1a: Plural and past tense removal.
        private void Step1a()
        {
            if (_word[_k] == 's')
            {
                if (EndsWith("sses")) { _k = _k - 2; }
                else if (EndsWith("ies")) { Replace("i", -1); }
                else if (_word[_k - 1] != 's' && EndsWith("s")) { _k--; }
            }
            if (EndsWith("eed"))
            {
                if (GetMeasure() > 0) _k--;
            }
            else if (EndsWith("ed") && ContainsVowel())
            {
                _k = _j;
                if (EndsWith("at")) { Replace("ate", -1); }
                else if (EndsWith("bl")) { Replace("ble", -1); }
                else if (EndsWith("iz")) { Replace("ize", -1); }
                else if (EndsInDoubleConsonant(_k))
                {
                    _k--;
                    if (_word[_k] == 'l' || _word[_k] == 's' || _word[_k] == 'z') _k++;
                }
                else if (CvC(_k))
                {
                    Replace("e", -1);
                }
            }
            else if (EndsWith("ing") && ContainsVowel())
            {
                _k = _j;
                if (EndsWith("at")) { Replace("ate", -1); }
                else if (EndsWith("bl")) { Replace("ble", -1); }
                else if (EndsWith("iz")) { Replace("ize", -1); }
                else if (EndsInDoubleConsonant(_k))
                {
                    _k--;
                    if (_word[_k] == 'l' || _word[_k] == 's' || _word[_k] == 'z') _k++;
                }
                else if (CvC(_k))
                {
                    Replace("e", -1);
                }
            }
        }

        // Step 1c: 'y' to 'i' conversion.
        private void Step1c()
        {
            if (EndsWith("y") && ContainsVowel())
            {
                _word[_k] = 'i';
            }
        }

        // Step 1b is empty, as its logic is merged into 1a for 'eed', 'ed', 'ing'.

        // Step 2: Double suffix replacement (for m > 0).
        private void Step2()
        {
            _j = _k;

            if (EndsWith("ational")) { Replace("ate", 0); }
            else if (EndsWith("tional")) { Replace("tion", 0); }
            else if (EndsWith("enci")) { Replace("ence", 0); }
            else if (EndsWith("anci")) { Replace("ance", 0); }
            else if (EndsWith("izer")) { Replace("ize", 0); }
            else if (EndsWith("abli")) { Replace("able", 0); }
            else if (EndsWith("alli")) { Replace("al", 0); }
            else if (EndsWith("entli")) { Replace("ent", 0); }
            else if (EndsWith("eli")) { Replace("e", 0); }
            else if (EndsWith("ousli")) { Replace("ous", 0); }
            else if (EndsWith("ization")) { Replace("ize", 0); }
            else if (EndsWith("ation")) { Replace("ate", 0); }
            else if (EndsWith("ator")) { Replace("ate", 0); }
            else if (EndsWith("alism")) { Replace("al", 0); }
            else if (EndsWith("iveness")) { Replace("ive", 0); }
            else if (EndsWith("fulness")) { Replace("ful", 0); }
            else if (EndsWith("ousness")) { Replace("ous", 0); }
            else if (EndsWith("aliti")) { Replace("al", 0); }
            else if (EndsWith("iviti")) { Replace("ive", 0); }
            else if (EndsWith("biliti")) { Replace("ble", 0); }
        }

        // Step 3: Further suffix removal (for m > 0).
        private void Step3()
        {
            _j = _k;

            if (EndsWith("icate")) { Replace("ic", 0); }
            else if (EndsWith("ative")) { Replace("", 0); }
            else if (EndsWith("alize")) { Replace("al", 0); }
            else if (EndsWith("iciti")) { Replace("ic", 0); }
            else if (EndsWith("ical")) { Replace("ic", 0); }
            else if (EndsWith("ful")) { Replace("", 0); }
            else if (EndsWith("ness")) { Replace("", 0); }
        }

        // Step 4: Removal of a large number of simple suffixes (for m > 1).
        private void Step4()
        {
            _j = _k;

            if (EndsWith("al")) { Replace("", 1); }
            else if (EndsWith("ance")) { Replace("", 1); }
            else if (EndsWith("ence")) { Replace("", 1); }
            else if (EndsWith("er")) { Replace("", 1); }
            else if (EndsWith("ic")) { Replace("", 1); }
            else if (EndsWith("able")) { Replace("", 1); }
            else if (EndsWith("ible")) { Replace("", 1); }
            else if (EndsWith("ant")) { Replace("", 1); }
            else if (EndsWith("ement")) { Replace("", 1); }
            else if (EndsWith("ment")) { Replace("", 1); }
            else if (EndsWith("ent")) { Replace("", 1); }
            else if (EndsWith("ou")) { Replace("", 1); }
            else if (EndsWith("ism")) { Replace("", 1); }
            else if (EndsWith("ate")) { Replace("", 1); }
            else if (EndsWith("iti")) { Replace("", 1); }
            else if (EndsWith("ous")) { Replace("", 1); }
            else if (EndsWith("ive")) { Replace("", 1); }
            else if (EndsWith("ize")) { Replace("", 1); }
            else if (EndsWith("ion"))
            {
                // Special case for 'ion' where the letter before must be 's' or 't'
                if (_j > 0 && (_word[_j] == 's' || _word[_j] == 't'))
                {
                    Replace("", 1);
                }
            }
        }

        // Step 5a: Removal of final 'e'.
        private void Step5a()
        {
            _j = _k;
            if (_word[_k] == 'e')
            {
                int m = GetMeasure();
                if (m > 1 || (m == 1 && !CvC(_k - 1)))
                {
                    _k--;
                }
            }
        }

        // Step 5b: Change 'll' to 'l' if m > 1.
        private void Step5b()
        {
            if (EndsInDoubleConsonant(_k) && _word[_k] == 'l')
            {
                if (GetMeasure() > 1)
                {
                    _k--;
                }
            }
        }
    }

    /// <summary>
    /// Example usage demonstration for the PorterStemmer.
    /// </summary>
}
// To run this, you would call WikiIndexBuilder.StemmerDemo.RunDemo()
// e.g. in your main application logic.
