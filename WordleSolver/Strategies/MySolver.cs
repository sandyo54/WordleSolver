using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordleSolver.Strategies
{
    public class MySolver : IWordleSolverStrategy
    {
        public List<string> WordList { get; set; } = new List<string>();
        private List<string> _remainingWords = new List<string>();

        public void Reset()
        {
            // Reset our pool to the full dictionary at the start of every game
            _remainingWords = new List<string>(WordList);
        }

        public string PickNextGuess(GuessResult previousResult)
        {
            // 1. Handle the opening move
            if (previousResult == null || previousResult.GuessNumber == 0)
            {
                return "ARISE"; // A top-tier starting word
            }

            // 2. Prune the search space based on the last result
            if (previousResult.IsValid)
            {
                // We need the string of the last guess. 
                // The GuessResult contains a list of all GuessResults so far.
                string lastGuess = GetWordFromState(previousResult);
                _remainingWords = FilterWords(_remainingWords, lastGuess, previousResult.LetterStatuses);
            }

            // 3. If only one word remains, pick it!
            if (_remainingWords.Count == 1) return _remainingWords[0];

            // 4. Scoring Heuristic: Pick the word that uses the most common letters 
            // in the remaining pool to narrow things down faster.
            return GetBestWordByFrequency(_remainingWords);
        }

        private List<string> FilterWords(List<string> candidates, string guess, LetterStatus[] feedback)
        {
            var result = new List<string>();
            foreach (var word in candidates)
            {
                bool keep = true;
                for (int i = 0; i < 5; i++)
                {
                    char g = guess[i];
                    char w = word[i];

                    if (feedback[i] == LetterStatus.Correct)
                    {
                        if (w != g) { keep = false; break; }
                    }
                    else if (feedback[i] == LetterStatus.Misplaced)
                    {
                        if (w == g || !word.Contains(g)) { keep = false; break; }
                    }
                    else if (feedback[i] == LetterStatus.Unused)
                    {
                        char greyChar = guess[i];
                        // Check if this same letter was Green or Yellow elsewhere in the guess
                        bool existsElsewhere = false;
                        for (int j = 0; j < 5; j++)
                        {
                            if (guess[j] == greyChar && feedback[j] != LetterStatus.Unused)
                            {
                                existsElsewhere = true;
                                break;
                            }
                        }

                        if (existsElsewhere)
                        {
                            // If it exists elsewhere, we ONLY eliminate words that have the char AT THIS SPOT
                            if (w == greyChar) { keep = false; break; }
                        }
                        else
                        {
                            // If it truly doesn't exist anywhere in the word, eliminate all words containing it
                            if (word.Contains(greyChar)) { keep = false; break; }
                        }
                    }
                }
                if (keep) result.Add(word);
            }
            return result;
        }

        private string GetBestWordByFrequency(List<string> pool)
        {
            // Safety check: If the pool is empty, we can't pick a word.
            // This prevents the "Sequence contains no elements" crash.
            if (pool == null || !pool.Any())
            {
                return "ARISE"; // Fallback to a safe word
            }

            var counts = pool.SelectMany(w => w.Distinct()).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

            return pool.OrderByDescending(w => w.Distinct().Sum(c => counts.ContainsKey(c) ? counts[c] : 0)).First();
        }

        private string GetWordFromState(GuessResult result)
        {
            // Accesses the 'Guesses' list in the result and returns the 
            // word string of the most recent guess.
            return result.Guesses.Last().ToString() ?? "";
        }
    }
}
