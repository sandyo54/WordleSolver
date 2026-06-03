namespace WordleSolver.Strategies
{
    /// <summary>
    /// ALGORITHM DESCRIPTION:
    /// This solver employs a two-phase deterministic elimination strategy combined with a letter-frequency
    /// positional heuristic to minimize the average number of guesses across 100 successive games.
    /// 
    /// 1. SEARCH SPACE ELIMINATION: After each guess, the solver prunes the '_remainingWords' pool by evaluating
    ///    every candidate against the precise constraints revealed by the engine feedback:
    ///    - Correct: Eliminates any word without that exact character at that exact index.
    ///    - Misplaced: Eliminates any word that doesn't contain the character, OR contains it at that exact index.
    ///    - Unused: Handled intelligently based on duplicate context. If the character appears elsewhere in the guess
    ///      as Correct/Misplaced, 'Unused' implies a maximum count constraint for that character. If it doesn't,
    ///      the character is completely purged from all positions.
    /// 
    /// 2. CANDIDATE SCORING (HEURISTIC): To pick the best word from the remaining valid pool, the solver builds a 
    ///    frequency map of characters across all currently surviving words. Words are scored by summing the 
    ///    frequencies of their unique characters. This maximizes information gain ("entropy approximation") per turn, 
    ///    effectively slicing the remaining search space down as aggressively as possible.
    /// </summary>
    public class FastInformationSolver : IWordleSolverStrategy
    {
        // Provided by the game framework containing the ~2300 5-letter words
        public List<string> WordList { get; set; } = new List<string>();

        private List<string> _remainingWords = new List<string>();
        private bool _isFirstGuess = true;

        // "ARISE" is mathematically one of the strongest opening words for letter frequency and vowel coverage
        private const string OptimalOpeningWord = "ARISE";

        public void Reset()
        {
            _remainingWords = new List<string>(WordList);
            _isFirstGuess = true;
        }

        public string PickNextGuess(GuessResult previousResult)
        {
            // Game start or previous invalid word safety net
            if (previousResult == null || previousResult.GuessNumber == 0)
            {
                return _remainingWords.Contains(OptimalOpeningWord) ? OptimalOpeningWord : _remainingWords.FirstOrDefault() ?? "ARISE";
            }

            // Process the constraints of the last valid guess to prune the search space
            if (previousResult.IsValid)
            {
                string lastGuess = previousResult.Guesses.Last().ToString(); // Assumes ToString returns the guessed word, or fetch appropriately
                                                                             // If your framework's GuessResult has an explicit string property like .Word, swap it here:
                                                                             // string lastGuess = previousResult.Word; 

                var statuses = previousResult.LetterStatuses;

                _remainingWords = FilterCandidates(_remainingWords, lastGuess, statuses);
            }

            // If we've squeezed the pool down to 0 or 1, return immediately
            if (_remainingWords.Count == 0) return OptimalOpeningWord;
            if (_remainingWords.Count == 1) return _remainingWords[0];

            // Score remaining words using letter frequencies of the *current* surviving pool
            return GetBestHeuristicWord(_remainingWords);
        }

        private List<string> FilterCandidates(List<string> currentPool, string guess, LetterStatus[] statuses)
        {
            var filtered = new List<string>();

            // Count occurrences of each letter in the guess that are confirmed to exist (Correct or Misplaced)
            var minLetterCounts = new Dictionary<char, int>();
            var exactMaxCounts = new Dictionary<char, int>();

            for (int i = 0; i < 5; i++)
            {
                char c = guess[i];
                if (statuses[i] == LetterStatus.Correct || statuses[i] == LetterStatus.Misplaced)
                {
                    if (minLetterCounts.ContainsKey(c)) minLetterCounts[c]++;
                    else minLetterCounts[c] = 1;
                }
            }

            // Identify "Unused" flags to establish absolute upper bounds on duplicate letters
            for (int i = 0; i < 5; i++)
            {
                char c = guess[i];
                if (statuses[i] == LetterStatus.Unused)
                {
                    // If it was marked unused but we found instances of it being Correct/Misplaced elsewhere in the word,
                    // then the min count we calculated is exactly the maximum allowed count of that letter.
                    if (minLetterCounts.ContainsKey(c))
                    {
                        exactMaxCounts[c] = minLetterCounts[c];
                    }
                    else
                    {
                        // Completely absent from the hidden word
                        exactMaxCounts[c] = 0;
                    }
                }
            }

            foreach (var word in currentPool)
            {
                bool isMatch = true;

                for (int i = 0; i < 5; i++)
                {
                    char guessChar = guess[i];
                    char wordChar = word[i];
                    LetterStatus status = statuses[i];

                    if (status == LetterStatus.Correct)
                    {
                        if (wordChar != guessChar) { isMatch = false; break; }
                    }
                    else if (status == LetterStatus.Misplaced)
                    {
                        // Must contain the letter, but NOT at this specific index
                        if (wordChar == guessChar || !word.Contains(guessChar)) { isMatch = false; break; }
                    }
                    else if (status == LetterStatus.Unused)
                    {
                        // If it's an absolute zero-count letter, the word must not contain it at all
                        if (exactMaxCounts.TryGetValue(guessChar, out int maxCount) && maxCount == 0)
                        {
                            if (word.Contains(guessChar)) { isMatch = false; break; }
                        }
                        else
                        {
                            // If it's a positional exclusion due to duplicate constraints
                            if (wordChar == guessChar) { isMatch = false; break; }
                        }
                    }
                }

                // Global count verification for tricky duplicate scenarios
                if (isMatch)
                {
                    foreach (var kvp in minLetterCounts)
                    {
                        int actualCount = word.Count(ch => ch == kvp.Key);
                        if (actualCount < kvp.Value) { isMatch = false; break; }
                    }
                    foreach (var kvp in exactMaxCounts)
                    {
                        int actualCount = word.Count(ch => ch == kvp.Key);
                        if (actualCount > kvp.Value) { isMatch = false; break; }
                    }
                }

                if (isMatch)
                {
                    filtered.Add(word);
                }
            }

            return filtered;
        }

        private string GetBestHeuristicWord(List<string> pool)
        {
            // Count letter frequencies within the remaining possible solutions
            var frequencyMap = new Dictionary<char, int>();
            foreach (var word in pool)
            {
                foreach (char c in word)
                {
                    if (frequencyMap.ContainsKey(c)) frequencyMap[c]++;
                    else frequencyMap[c] = 1;
                }
            }

            string bestWord = pool[0];
            int highestScore = -1;

            foreach (var word in pool)
            {
                // Score word based on distinct letters to maximize entropy gain
                int currentScore = word.Distinct().Sum(c => frequencyMap.TryGetValue(c, out int count) ? count : 0);

                if (currentScore > highestScore)
                {
                    highestScore = currentScore;
                    bestWord = word;
                }
            }

            return bestWord;
        }
    }
}
