# LLMUnity Sample Scene Diagrams

This document maps the sample scenes in `Assets/LLMUnity/Samples/` to their runtime flow and package architecture. Each section includes a Mermaid diagram showing how the sample script interacts with `LLMAgent`, `LLM`, `RAG`, and UI.

## Package architecture summary

- `LLM` component: loads and manages the actual model backend. Can be local or remote.
- `LLMAgent` component: chat-focused wrapper around `LLMClient`; handles prompt/chat state, callbacks, and history.
- `RAG` component: retrieval-augmented generation wrapper; builds embeddings with an LLM and performs semantic search.
- `LLMClient.cs`: base abstraction for completion parameters, tokenization, canceling, and request lifecycle.

## 1. SimpleInteraction sample

- Script: `Assets/LLMUnity/Samples/SimpleInteraction/SimpleInteraction.cs`
- Scene: `Assets/LLMUnity/Samples/SimpleInteraction/Scene.unity`

```mermaid
flowchart TD
    UIInput[InputField]
    UIOutput[Text Output]
    SimpleInteraction[SimpleInteraction.cs]
    LLMAgent[LLMAgent]
    LLM[LLM model backend]

    UIInput -->|submit message| SimpleInteraction
    SimpleInteraction -->|Chat(message)| LLMAgent
    LLMAgent -->|calls| LLM
    LLM -->|stream/complete reply| LLMAgent
    LLMAgent -->|SetAIText(reply)| SimpleInteraction
    SimpleInteraction -->|update UI| UIOutput
```

## 2. ChatBot sample

- Script: `Assets/LLMUnity/Samples/ChatBot/ChatBot.cs`
- Scene: `Assets/LLMUnity/Samples/ChatBot/Scene.unity`

```mermaid
flowchart TD
    InputBubble[InputBubble UI]
    ChatBot[ChatBot.cs]
    BubbleUI[Bubble UI manager]
    LLMAgent[LLMAgent]
    LLM[LLM model backend]
    StopButton[Stop button]

    InputBubble -->|submit text| ChatBot
    ChatBot -->|Warmup()| LLMAgent
    ChatBot -->|Chat(message)| LLMAgent
    LLMAgent --> LLM
    LLM -->|reply stream| LLMAgent
    LLMAgent -->|Update bubble| ChatBot
    StopButton -->|CancelRequests()| ChatBot
    ChatBot -->|CancelRequests()| LLMAgent
```

## 3. MobileDemo sample

- Script: `Assets/LLMUnity/Samples/MobileDemo/MobileDemo.cs`
- Scene: `Assets/LLMUnity/Samples/MobileDemo/Scene.unity`

```mermaid
flowchart TD
    DownloadPanel[Download Panel]
    ProgressUI[Progress bar/Text]
    MobileDemo[MobileDemo.cs]
    LLMAgent[LLMAgent]
    LLM[LLM model backend]
    ChatPanel[Chat Panel]

    MobileDemo -->|WaitUntilModelSetup| LLM
    LLM -->|download/setup| MobileDemo
    MobileDemo -->|Warmup()| LLMAgent
    MobileDemo -->|Chat(message)| LLMAgent
    LLMAgent --> LLM
    LLM -->|reply| LLMAgent
    MobileDemo -->|update text| ChatPanel
```

## 4. MultipleCharacters sample

- Script: `Assets/LLMUnity/Samples/MultipleCharacters/MultipleCharacters.cs`
- Scene: `Assets/LLMUnity/Samples/MultipleCharacters/Scene.unity`

```mermaid
flowchart TD
    UIInput[InputField]
    AgentSelect[Dropdown]
    MultipleCharacters[MultipleCharacters.cs]
    LLMAgent1[LLMAgent #1]
    LLMAgent2[LLMAgent #2]
    LLM1[LLM model #1]
    LLM2[LLM model #2]
    AIText1[Text #1]
    AIText2[Text #2]

    UIInput -->|submit message| MultipleCharacters
    MultipleCharacters -->|select agent| AgentSelect
    AgentSelect --> MultipleCharacters
    MultipleCharacters -->|Chat(message)| LLMAgent1
    MultipleCharacters -->|Chat(message)| LLMAgent2
    LLMAgent1 --> LLM1
    LLMAgent2 --> LLM2
    LLMAgent1 -->|reply| AIText1
    LLMAgent2 -->|reply| AIText2
```

## 5. FunctionCalling sample

- Script: `Assets/LLMUnity/Samples/FunctionCalling/FunctionCalling.cs`
- Scene: `Assets/LLMUnity/Samples/FunctionCalling/Scene.unity`

```mermaid
flowchart TD
    InputField[InputField]
    FunctionCalling[FunctionCalling.cs]
    LLMAgent[LLMAgent]
    LLM[LLM model backend]
    Functions[Functions static methods]
    AIText[Text Output]

    InputField -->|submit text| FunctionCalling
    FunctionCalling -->|ConstructPrompt(message)| FunctionCalling
    FunctionCalling -->|Chat(prompt)| LLMAgent
    LLMAgent --> LLM
    LLM -->|function name| LLMAgent
    LLMAgent -->|reply| FunctionCalling
    FunctionCalling -->|CallFunction(name)| Functions
    FunctionCalling -->|display result| AIText
```

## 6. RAG sample

- Script: `Assets/LLMUnity/Samples/RAG/RAG_Sample.cs`
- Scene: `Assets/LLMUnity/Samples/RAG/RAG_Scene.unity`

```mermaid
flowchart TD
    InputField[InputField]
    RAGSample[RAGSample.cs]
    RAG[RAG]
    HamletText[TextAsset: Hamlet]
    AIText[Text Output]
    LLMEmbedder[LLM embedder]

    RAGSample -->|LoadPhrases()| HamletText
    RAGSample -->|Load / Create embeddings| RAG
    RAG -->|use embeddings| LLMEmbedder
    InputField -->|submit message| RAGSample
    RAGSample -->|Search(query)| RAG
    RAG -->|similar phrase| RAGSample
    RAGSample -->|display| AIText
```

## 7. RAG+LLM sample

- Script: `Assets/LLMUnity/Samples/RAG/RAGAndLLM_Sample.cs`
- Scene: `Assets/LLMUnity/Samples/RAG/RAGAndLLM_Scene.unity`

```mermaid
flowchart TD
    InputField[InputField]
    RAGAndLLMSample[RAGAndLLMSample.cs]
    RAG[RAG]
    LLMAgent[LLMAgent]
    AIText[Text Output]
    ParaphraseToggle[Toggle]

    InputField -->|submit message| RAGAndLLMSample
    RAGAndLLMSample -->|Search(query)| RAG
    RAG -->|similar phrase| RAGAndLLMSample
    RAGAndLLMSample -->|if ParaphraseWithLLM| LLMAgent
    LLMAgent --> LLM[LLM model backend]
    LLMAgent -->|reply| AIText
    RAGAndLLMSample -->|or direct display| AIText
```

## 8. KnowledgeBaseGame sample

- Script: `Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGame.cs`
- Scene: `Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGameScene.unity`

```mermaid
flowchart TD
    CharacterSelect[Character dropdown]
    PlayerText[InputField]
    AIText[Text Output]
    KnowledgeBaseGame[KnowledgeBaseGame.cs]
    LLMAgent[LLMAgent]
    RAG[RAG]
    LLM[LLM model backend]
    BotData[TextAsset bot knowledge]

    CharacterSelect -->|change bot| KnowledgeBaseGame
    PlayerText -->|submit question| KnowledgeBaseGame
    KnowledgeBaseGame -->|Retrieval(question)| RAG
    RAG -->|Search(query)| RAG
    RAG -->|similar questions| KnowledgeBaseGame
    KnowledgeBaseGame -->|ConstructPrompt()| KnowledgeBaseGame
    KnowledgeBaseGame -->|Chat(prompt)| LLMAgent
    LLMAgent --> LLM
    LLM -->|reply| LLMAgent
    LLMAgent -->|SetAIText| KnowledgeBaseGame
    KnowledgeBaseGame -->|display| AIText
    BotData -->|load answers| KnowledgeBaseGame
```

## Notes on diagrams

- `LLMAgent` always represents the chat agent component exposed in the scene inspector.
- `LLM` represents the underlying model backend, which may be a local `LLM` component or a remote server.
- `RAG` represents retrieval augmentation, which typically uses an embedded `LLM` to build/search embeddings and optionally return chunks.
- Every sample that uses `LLMAgent.Chat(...)` follows the same core pattern: UI -> sample controller -> `LLMAgent` -> `LLM` -> reply -> UI.
- `RAG` samples additionally add semantic search before UI output.
