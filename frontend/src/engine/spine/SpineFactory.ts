import { Assets } from 'pixi.js';
import { Spine } from 'pixi-spine';
import type { SpineAssetDefinition } from './SpineAssetConfig';

export class SpineFactory {
  async load(definition: SpineAssetDefinition): Promise<void> {
    await Assets.load(definition.skelPath);
    await Assets.load(definition.atlasPath);
    await Assets.load(definition.pngPath);
  }

  async createInstance(skelPath: string): Promise<Spine> {
    const spineData = await Assets.load(skelPath);
    const spine = new Spine(spineData);
    spine.autoUpdate = true;
    return spine;
  }
}

