# 🚗 Car Racing — SAC vs PPO Complete Guide

## Files Jo Aapko Mile Hain:
- `CarAgent.cs` → Unity mein car agent script
- `CheckpointScript.cs` → Track checkpoints ke liye
- `sac_car.yaml` → SAC training config
- `ppo_car.yaml` → PPO training config  
- `compare_results.py` → Results compare karne ke liye

---

## Step 1: ML-Agents Install Karo

### Unity Package Manager:
```
Window → Package Manager → + → Add by name:
com.unity.ml-agents
```

### Python:
```bash
pip install mlagents torch
```

---

## Step 2: Unity Setup

### A) CarAgent.cs lagao:
1. Car GameObject select karo
2. `RobotController.cs` **DISABLE** karo (Inspector mein uncheck)
3. `CarAgent.cs` **ADD** karo (Add Component)
4. Inspector mein assign karo:
   - Wheel Colliders (4)
   - Wheel Transforms (4)
   - RayCast Sensors (Front, Left, Right)

### B) Behavior Parameters component add karo:
- Behavior Name: `CarAgent`
- Space Size: `14` (observations)
- Continuous Actions: `2` (throttle + steering)
- Discrete Actions: `0`

### C) Decision Requester add karo:
- Decision Period: `5`

### D) Checkpoints banao:
1. Track pe Empty GameObjects banao
2. Unhe `CheckpointScript.cs` lagao
3. Tag: `Checkpoint`
4. Index: 0, 1, 2, 3... (order mein)
5. CarAgent Inspector mein Checkpoints array fill karo

### E) Obstacles Layer check karo:
- Aapke cubes already "Obs" layer pe hain ✅
- CarAgent collision detection automatically kaam karega

---

## Step 3: SAC Training Shuru Karo

```bash
# SAC training
mlagents-learn sac_car.yaml --run-id=SAC_Run1

# Unity mein Play dabao — training shuru!
```

Training complete hone ke baad:

```bash
# PPO training
mlagents-learn ppo_car.yaml --run-id=PPO_Run1

# Unity mein Play dabao
```

---

## Step 4: Results Compare Karo

```bash
# Tensorboard se live dekho
tensorboard --logdir=results/

# Ya Python script se graph banao
python compare_results.py
```

---

## Reward System Samajhna:

| Event | Reward |
|-------|--------|
| Aage badhna (speed) | +0.001 per step |
| Checkpoint pass karna | +1.0 |
| Lap complete karna | +5.0 |
| Obstacle se takrana | -1.0 + EndEpisode |
| Track se bahar jaana | -2.0 + EndEpisode |
| Time limit exceed | -0.5 + EndEpisode |
| Har step penalty | -0.001 |

---

## SAC vs PPO — Expected Results:

```
Steps:      0    200k   400k   600k   800k   1M
SAC:       -50   -20     +5    +15    +25    +32
PPO:       -50   -10     +5    +10    +18    +22
```

**SAC pehle slow hai lekin eventually PPO se better ho jaata hai!**
