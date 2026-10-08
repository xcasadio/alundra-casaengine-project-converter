#!/bin/sh
# Runs every mutation of the T1 prototype in the PHYSICAL COPY only, one after the other.
V="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-la/verify"
B="$V/build/engine"
FILES="CasaEngine/Framework/Audio/Streaming/WavFormatInfo.cs CasaEngine/Framework/Audio/Streaming/WavStreamReader.cs CasaEngine/Framework/Audio/SoundAsset.cs CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs CasaEngine.EditorServices/EditorAssetJsonSerializer.cs CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs"
cd "$B" || exit 1
for m in $MUTATIONS; do
  for f in $FILES; do cp "$V/pristine/$f" "$B/$f"; done
  python -I "$V/proto/apply_t1.py" "$B" "$m" || { echo "APPLY FAILED $m"; continue; }
  dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug -nodeReuse:false -p:UseSharedCompilation=false -v:quiet > "$V/results/mut_$m.build.log" 2>&1
  grep -q " 0 Erreur" "$V/results/mut_$m.build.log" || { echo "BUILD FAILED $m"; continue; }
  dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build -c Debug --blame-hang-timeout 60s \
    --filter "FullyQualifiedName~CasaEngine.Tests.Audio|FullyQualifiedName~SoundAssetInspectorPanel" \
    --logger "trx;LogFileName=mut_$m.trx" --results-directory "$V/results" > "$V/results/mut_$m.log" 2>&1
  echo "done $m"
done
# back to the plain T1 prototype
for f in $FILES; do cp "$V/pristine/$f" "$B/$f"; done
python -I "$V/proto/apply_t1.py" "$B"
echo ALL_DONE
