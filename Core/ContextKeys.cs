namespace PokeChat.Core;

public static class ContextKeys
{
    public const string PendingClarificationWord = "pending_clarification_word";
    public const string PendingClarificationSuggestion = "pending_clarification_suggestion";
    public const string UnknownWords = "unknown_words";
    public const string LastResponse = "last_response";
    public const string UserName = "user_name";
    public const string PendingDictionaryWord = "pending_dictionary_word";
    public const string SubjectCategory = "subject_category";
    public const string ObjectCategory = "object_category";
    public const string RecentlyUsedFacts = "recently_used_facts";
    public const string ContextFollowUpCount = "context_follow_up_count";
    public const string PendingReset = "pending_reset";
    public const string CurrentSentiment = "current_sentiment";
    public const string LastSentimentIntensity = "last_sentiment_intensity";
    public const string PreviousSentiment = "previous_sentiment";
    public const string PendingSentimentFollowUp = "pending_sentiment_followup";
    public const string PendingSentimentIntensity = "pending_sentiment_intensity";
    public const string SentimentTurnCount = "sentiment_turn_count";
    public const string CurrentTimeContext = "current_time_context";
    public const string InferenceDepth = "inference_depth";
    public const string LastContradiction = "last_contradiction";
    public const string InferredGeneralisation = "inferred_generalisation";
    public const string SessionId = "session_id";
    public const string LastRuleId = "last_rule_id";
    public const string LastRuleIsLearned = "last_rule_is_learned";
    public const string PendingCorrectionPattern = "pending_correction_pattern";
    public const string PendingCorrectionResponse = "pending_correction_response";
    public const string LastUserInput = "last_user_input";
    public const string TopicStackLength = "topic_stack_length";
    public const string LastTopicSubject = "last_topic_subject";
    public const string LastTopicObject = "last_topic_object";
    public const string TopicReferenceCount = "topic_reference_count";
    public const string CurrentResponseCategory = "current_response_category";
    public const string PreviousResponseCategory = "previous_response_category";
    public const string LastResponseHadSvo = "last_response_had_svo";
    public const string AdaptiveResponseWeighting = "adaptive_response_weighting";
    public const string PendingClassificationWord = "pending_classification_word";
    public const string PendingClassificationCount = "pending_classification_count";
    public const string PendingPlaceWord = "pending_place_word";
    public const string PendingLLMOffer = "pending_llm_offer";
    public const string LLMOriginalInput = "llm_original_input";
    public const string PendingDictionarySave = "pending_dictionary_save";
    public const string GameModeActive = "game_mode_active";
    public const string GameStory = "game_story";
    public const string GameTurnCount = "game_turn_count";
}