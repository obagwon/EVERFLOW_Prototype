# Historical preparation snapshots

These files were prepared before the live Editor integration was enabled on
2026-10-17. They are not Unity sources and are not the current implementation.
Edit `Assets/EverflowMap/Runtime`, `Assets/EverflowMap/Editor`, and
`Assets/Scenes/Everflow_Map_MVP.unity`. Do not copy these snapshots back over them.

The `--staging` compiler option belonged to the preparation phase and now
refuses to run over the integrated Assets. Use
`python -B tools/verify_mvp_experiment.py` for current Assets sources.
