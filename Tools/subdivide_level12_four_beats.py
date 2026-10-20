"""Split the current Bell/Chords slices into exact four-beat PCM WAVs."""
import csv
import hashlib
import io
import re
import uuid
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / 'Assets/Audio/Lv.1&2'
BPM = 100
FRAMES = 105840  # 44100 * (60 / 100) * 4 = 2.4 seconds


def meta(path, template):
    output = Path(str(path) + '.meta')
    if not output.exists():
        output.write_text(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, template), encoding='utf-8')


def main():
    scene = (ROOT / 'Assets/Scenes/Map.unity').read_text(encoding='utf-8')
    assert re.findall(r'^  bpm: (\d+)$', scene, re.M) == [str(BPM)]
    for instrument, source_count, source_frames in [('Bell', 8, FRAMES * 4), ('Chords', 4, FRAMES * 2)]:
        source_dir = AUDIO / f'{instrument}_16BeatSegments'
        sources = sorted(source_dir.glob('*.wav'), key=lambda p: int(p.stem.rsplit('_', 1)[1]))
        assert len(sources) == source_count, (source_dir, len(sources))
        assert [int(p.stem.rsplit('_', 1)[1]) for p in sources] == list(range(1, source_count + 1))
        dest = AUDIO / f'{instrument}_4BeatSegments'
        dest.mkdir(exist_ok=True)
        meta(dest, Path(str(source_dir) + '.meta').read_text(encoding='utf-8'))
        rows = []
        index = 0
        for source in sources:
            original = source.read_bytes()
            with wave.open(io.BytesIO(original), 'rb') as wav:
                params = wav.getparams()
                assert (params.framerate, params.nchannels, params.sampwidth, params.comptype) == (44100, 2, 3, 'NONE')
                assert params.nframes == source_frames, (source, params.nframes)
                pcm = wav.readframes(params.nframes)
            # Lossless import for the newly generated assets; do not change source importers.
            importer = Path(str(source) + '.meta').read_text(encoding='utf-8')
            importer = re.sub(r'compressionFormat: \d+', 'compressionFormat: 0', importer)
            importer = re.sub(r'normalize: \d+', 'normalize: 0', importer)
            rebuilt = bytearray()
            for start in range(0, params.nframes, FRAMES):
                index += 1
                part = pcm[start * 6:(start + FRAMES) * 6]
                output = dest / f'Lv1_{instrument}_4Beats_{index:02d}.wav'
                if not output.exists():
                    with wave.open(str(output), 'wb') as wav:
                        wav.setparams(params)
                        wav.writeframes(part)
                with wave.open(str(output), 'rb') as wav:
                    assert (wav.getframerate(), wav.getnchannels(), wav.getsampwidth(), wav.getnframes()) == (44100, 2, 3, FRAMES)
                    verified = wav.readframes(FRAMES)
                    assert verified == part, f'Existing output differs: {output}'
                    rebuilt.extend(verified)
                meta(output, importer)
                rows.append([output.name, source.name, start, start + FRAMES, (index - 1) * 4,
                             FRAMES, '2.4', hashlib.sha256(part).hexdigest()])
            assert bytes(rebuilt) == pcm, f'Reconstruction failed: {source}'
            assert source.read_bytes() == original, f'Source changed: {source}'
        with (dest / 'segments.csv').open('w', newline='', encoding='utf-8') as handle:
            writer = csv.writer(handle)
            writer.writerow(['output', 'source_slice', 'source_start_frame_inclusive', 'source_end_frame_exclusive',
                             'sequence_start_beat_zero_based', 'frames', 'seconds', 'pcm_sha256'])
            writer.writerows(rows)
        (dest / 'README.txt').write_text(
            f'{BPM} BPM; {index} segments; 4 beats / 2.4 seconds / {FRAMES} frames each.\n'
            f'Source: ../{source_dir.name}/, ordered by final numeric suffix.\n'
            f'Actual source slice length: {source_frames / FRAMES * 4:g} beats.\n'
            '44.1 kHz, stereo, 24-bit PCM. Exact sample cuts; no fades, normalization, resampling or added padding.\n'
            'Every group of output slices reconstructs its source slice byte-for-byte in PCM data.\n'
            'Original slices retained, including any silence already present. No repeated segments removed.\n'
            'segments.csv records source offsets and PCM SHA-256 hashes.\n', encoding='utf-8')
        text_meta = 'fileFormatVersion: 2\nguid: 0\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        for filename in ['README.txt', 'segments.csv']:
            meta(dest / filename, text_meta)
        print(f'{instrument}: PASS, {source_count} input slices -> {index} x 4 beats (2.4 seconds); PCM reconstruction exact.')


if __name__ == '__main__':
    main()
