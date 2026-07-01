import os
import json
import urllib.request
import time

def call_llm(prompt):
    url = 'http://localhost:11435/v1/chat/completions'
    data = {
        "model": "default-llm",
        "messages": [
            {"role": "system", "content": "You are the Village Blacksmith. Stay in character. You are grumpy and straightforward. You value hard work."},
            {"role": "user", "content": prompt}
        ],
        "temperature": 0.7
    }
    
    req = urllib.request.Request(url, data=json.dumps(data).encode('utf-8'), headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req) as response:
            result = json.loads(response.read().decode('utf-8'))
            return result['choices'][0]['message']['content']
    except Exception as e:
        return f"[Error: {e}]"

# Simulated retrieved data
rag_knowledge = "- Iron ore is found in the northern mountains."
cognee_knowledge = "{\"episodic\": \"The player gave you 5 gold coins yesterday to fix their shield. You promised it would be ready today.\"}"
player_message = "What can you tell me about my shield?"

combined_knowledge = "Relevant knowledge for Blacksmith:\n"
combined_knowledge += f"{rag_knowledge}\n"
combined_knowledge += f"Additional Context: {cognee_knowledge}\n"

final_prompt = f"{combined_knowledge}\nPlayer message: {player_message}\n\nReply in character. Use the knowledge above only if it is relevant and avoid mentioning this instruction block."

print("\nCalling LLM...")
response = call_llm(final_prompt)
print(f"\n[Blacksmith]: {response}")

with open("cognee_memory_simulation.log", "w") as f:
    f.write("--- NPCDialogueManager Cognee Integration Simulation ---\n")
    f.write(f"Final Prompt Sent to LLM:\n{final_prompt}\n")
    f.write(f"\n[Blacksmith]: {response}\n")
