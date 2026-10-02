import Phaser from 'phaser'
import type { AnimalStateEnvelope, AnimalDto } from '../types/animal'
import type { AnimalCollectDone, AnimalHelpDone, AnimalStealDone } from '../types/animalEvents'
import { calculateAnimalLayout, type WorldBounds } from './animalLayout'
import {
  CHICKEN_ATLAS,
  CHICKEN_METRICS,
  COW_ATLAS,
  COW_METRICS,
  cowFootOrigin,
  SHEEP_ATLAS,
  LAMB_ATLAS,
  SHEEP_METRICS,
  sheepFootOrigin,
  PIG_ATLAS,
  PIG_METRICS,
  pigFootOrigin,
  MILK_ATLAS,
  MILK_METRICS,
  DEFAULT_ENCLOSURE_STYLE,
  CHICKEN_NESTS_ATLAS,
  ENCLOSURE_STYLES,
} from './animalAssets'
import {
  matchesAnimalFeedback,
  animalActionCaption,
  resolveCowMotion,
  resolveSheepMotion,
  resolvePigMotion,
  resolvePigFrame,
  resolveChickenMotion,
  resolveAnimalSemanticScale,
  resolveAnimalVisual,
} from './animalVisuals'

type Visual = {
  animal: AnimalDto
  sprite: Phaser.GameObjects.Image
  product?: Phaser.GameObjects.Image
  shadow: Phaser.GameObjects.Ellipse
  target: Phaser.GameObjects.Zone
  frame: string
  collectedCycleId?: string
}

/** Owns presentation objects only. All stage/cycle values come from React. */
export class AnimalEnclosureWorld {
  readonly layout: ReturnType<typeof calculateAnimalLayout>
  private readonly scene: Phaser.Scene
  private readonly farmId: string
  private readonly inputAllowed: () => boolean
  private readonly reduceMotion: boolean
  private state: AnimalStateEnvelope | null = null
  private objects: Phaser.GameObjects.GameObject[] = []
  private animals = new Map<string, Visual>()
  private feedback = new Set<Phaser.GameObjects.GameObject>()
  private completed: Set<string>

  constructor(scene: Phaser.Scene, farmId: string, plots: WorldBounds,
    inputAllowed: () => boolean, reduceMotion: boolean) {
    this.scene = scene
    this.farmId = farmId
    this.layout = calculateAnimalLayout(plots)
    this.inputAllowed = inputAllowed
    this.reduceMotion = reduceMotion
    this.completed = scene.registry.get('animalVisualCompletions') ?? new Set<string>()
    scene.registry.set('animalVisualCompletions', this.completed)
    scene.events.on(Phaser.Scenes.Events.UPDATE, this.update)
    window.addEventListener('animal:collect:done', this.onCollect)
    window.addEventListener('animal:help:done', this.onHelp)
    window.addEventListener('animal:steal:done', this.onSteal)
  }

  get visible() { return Boolean(this.state?.enclosure) }

  sync(state: AnimalStateEnvelope | null | undefined) {
    if (state && state.farmId !== this.farmId) return
    const previousId = this.state?.enclosure?.id
    this.state = state ?? null
    if (previousId !== state?.enclosure?.id) this.clearObjects()
    if (!state?.enclosure) return
    if (this.objects.length === 0) this.createEnclosure()

    const live = new Set(state.enclosure.animals.map(animal => animal.id))
    for (const [id, visual] of this.animals) {
      if (!live.has(id)) { this.destroyAnimal(visual); this.animals.delete(id) }
    }
    for (const animal of state.enclosure.animals) {
      // Future catalog entries need their own approved art, never extra hens.
      if (animal.animalTypeId !== 'chicken' && animal.animalTypeId !== 'cow' &&
        animal.animalTypeId !== 'sheep' && animal.animalTypeId !== 'pig') continue
      let visual = this.animals.get(animal.id)
      if (!visual) {
        visual = this.createAnimal(animal)
        this.animals.set(animal.id, visual)
      } else if (visual.animal.cycle?.id !== animal.cycle?.id) {
        visual.collectedCycleId = visual.animal.cycle?.id
      }
      visual.animal = animal
      const presentation = resolveAnimalVisual(animal)
      if (visual.product && presentation.productFrame) visual.product.setFrame(presentation.productFrame)
    }
    this.update(this.scene.time.now)
  }

  private createEnclosure() {
    const { structure } = this.layout
    const enclosureStyle = ENCLOSURE_STYLES[DEFAULT_ENCLOSURE_STYLE]
    const enclosure = this.scene.add.image(
      structure.x,
      structure.y,
      enclosureStyle.atlas,
      enclosureStyle.frame,
    )
      .setOrigin(structure.originX, structure.originY)
      .setDisplaySize(structure.width, structure.height)
      .setDepth(290)
    this.objects.push(enclosure)
  }

  private createAnimal(animal: AnimalDto): Visual {
    const cow = animal.animalTypeId === 'cow'
    const sheep = animal.animalTypeId === 'sheep'
    const pig = animal.animalTypeId === 'pig'
    const position = pig ? this.layout.pig : sheep ? this.layout.sheep : cow ? this.layout.cow : this.layout.chicken
    const shadow = this.scene.add.ellipse(position.x, position.y, pig ? 28 : sheep ? 25 : cow ? 27 : 17, 6, 0x4a462a, 0.22).setDepth(300)
    const presentation = resolveAnimalVisual(animal)
    const texture = pig ? PIG_ATLAS : sheep ? animal.stage === 'lamb' ? LAMB_ATLAS : SHEEP_ATLAS : cow ? COW_ATLAS : CHICKEN_ATLAS
    const firstFrame = pig ? resolvePigFrame(animal, 'idle')
      : sheep ? animal.stage === 'lamb' ? 'lamb-idle' : `${presentation.coat}-idle` : `${animal.stage}-idle`
    const sprite = this.scene.add.image(position.x, position.y, texture, firstFrame).setDepth(330)
    const productPosition = cow ? this.layout.milk : this.layout.nest
    const product = sheep || pig ? undefined : this.scene.add.image(productPosition.x, productPosition.y,
      cow ? MILK_ATLAS : CHICKEN_NESTS_ATLAS, presentation.productFrame!)
      .setOrigin(cow ? MILK_METRICS.originX : 0.5, cow ? MILK_METRICS.originY : 0.85).setDepth(350)
    // Separate species targets include their product without overlapping each other.
    const sector = this.layout.reserved[pig ? 3 : sheep ? 2 : cow ? 1 : 0]
    // The sheep target extends from its hooves to the head while staying clear
    // of the cow and the fourth reserved sector.
    const target = this.scene.add.zone(sector.x, sector.y - (pig ? 12 : sheep ? 15 : cow ? 12 : 0),
      sector.width, pig ? 38 : sheep ? 42 : cow ? 44 : 34).setDepth(460).setInteractive()
    target.on('pointerup', () => {
      if (!this.inputAllowed()) return
      const screenPosition = this.resolveScreenPosition(target.x, target.y)
      window.dispatchEvent(new CustomEvent('animal:select', {
        detail: {
          farmId: this.farmId,
          animalId: animal.id,
          ...screenPosition,
        },
      }))
    })
    return { animal, sprite, product, shadow, target, frame: '' }
  }

  private resolveScreenPosition(worldX: number, worldY: number) {
    const camera = this.scene.cameras.main
    const canvas = this.scene.game.canvas
    const rect = canvas.getBoundingClientRect()
    const gameSize = this.scene.scale.gameSize

    if (rect.width <= 0 || rect.height <= 0 || gameSize.width <= 0 || gameSize.height <= 0) {
      return {}
    }

    const canvasX = camera.x + (worldX - camera.worldView.x) * camera.zoom
    const canvasY = camera.y + (worldY - camera.worldView.y) * camera.zoom

    return {
      screenX: rect.left + canvasX * (rect.width / gameSize.width),
      screenY: rect.top + canvasY * (rect.height / gameSize.height),
    }
  }

  private update = (time: number) => {
    const semanticScale = resolveAnimalSemanticScale(
      this.scene.cameras.main.zoom,
    )

    for (const visual of this.animals.values()) {
      const { stage } = visual.animal
      const cow = visual.animal.animalTypeId === 'cow'
      const sheep = visual.animal.animalTypeId === 'sheep'
      const pig = visual.animal.animalTypeId === 'pig'
      const lamb = sheep && stage === 'lamb'
      const metrics = pig ? PIG_METRICS[stage === 'piglet' ? 'piglet' : 'adult']
        : sheep ? SHEEP_METRICS[lamb ? 'lamb' : 'adult']
        : cow ? COW_METRICS[stage === 'adult' ? 'adult' : 'calf']
          : CHICKEN_METRICS[stage === 'adult' ? 'adult' : 'chick']
      const position = pig ? this.layout.pig : sheep ? this.layout.sheep : cow ? this.layout.cow : this.layout.chicken
      const pigMotion = pig ? resolvePigMotion(time, this.reduceMotion) : null
      const motion = pigMotion ?? (sheep ? resolveSheepMotion(time, this.reduceMotion)
        : cow ? resolveCowMotion(time, this.reduceMotion) : resolveChickenMotion(time, this.reduceMotion))
      const frame = pigMotion ? resolvePigFrame(visual.animal, pigMotion.pose)
        : sheep ? `${lamb ? 'lamb' : resolveAnimalVisual(visual.animal).coat}-${motion.pose}`
        : `${stage}-${motion.pose}`
      const texture = pig ? PIG_ATLAS
        : sheep ? lamb ? LAMB_ATLAS : SHEEP_ATLAS : cow ? COW_ATLAS : CHICKEN_ATLAS
      if (visual.sprite.texture.key !== texture) {
        visual.sprite.setTexture(texture, frame)
        visual.frame = ''
      }
      if (visual.frame !== frame) {
        if (pig) {
          const origin = pigFootOrigin(frame)
          visual.sprite.setFrame(frame).setOrigin(origin.x, origin.y)
        } else {
          visual.sprite.setFrame(frame).setOrigin(0.5,
            sheep ? sheepFootOrigin(frame, lamb) : cow ? cowFootOrigin(frame)
              : CHICKEN_METRICS[stage === 'adult' ? 'adult' : 'chick'].originY)
        }
        visual.frame = frame
      }
      visual.sprite
        .setPosition(
          position.x + motion.offset,
          position.y + motion.offset / 2,
        )
        .setDepth(330 + motion.offset / 2)
        .setScale(metrics.scale * semanticScale)
        .setFlipX(motion.flipX)

        const shadowOffsetX = 0
        const shadowOffsetY = pig || sheep || cow ? -3 : 0
      visual.shadow.setPosition(
        visual.sprite.x + shadowOffsetX,
        visual.sprite.y + shadowOffsetY
      )
      .setDisplaySize((pig ? 28 : sheep ? 25 : cow ? 27 : 17) * semanticScale, 6 * semanticScale)
      if (cow) visual.product?.setScale(MILK_METRICS.scale * semanticScale)
      else if (!sheep && !pig) visual.product?.setDisplaySize(28 * semanticScale, 19 * semanticScale)
    }
  }

  private onCollect = (event: Event) => this.confirm('collect', (event as CustomEvent<AnimalCollectDone>).detail)
  private onHelp = (event: Event) => this.confirm('help', (event as CustomEvent<AnimalHelpDone>).detail)
  private onSteal = (event: Event) => this.confirm('steal', (event as CustomEvent<AnimalStealDone>).detail)

  private confirm(action: 'collect' | 'help' | 'steal', result: AnimalCollectDone | AnimalHelpDone | AnimalStealDone) {
    if (!result.animalId || !result.cycleId) return
    const visual = this.animals.get(result.animalId)
    if (!visual || this.completed.has(result.operationId) || !matchesAnimalFeedback({ ...result, animalId: result.animalId, cycleId: result.cycleId },
      this.farmId, visual.animal.id, visual.animal.cycle?.id, visual.collectedCycleId, action)) return
    this.completed.add(result.operationId)
    if (this.completed.size > 256) this.completed.delete(this.completed.values().next().value!)
    const amount = action === 'help' ? result.ownerBonusAdded : result.quantity
    const caption = animalActionCaption(action, result.productId, amount)
    const text = this.scene.add.text(visual.product?.x ?? visual.sprite.x,
      (visual.product?.y ?? visual.sprite.y) - 20,
      `${caption}${action === 'help' && result.coinsGained ? `\n+${result.coinsGained} 🪙` : ''}${result.xpGained ? `\n+${result.xpGained} XP` : ''}`, {
        fontFamily: 'Arial, sans-serif', fontSize: '12px', fontStyle: 'bold',
        color: action === 'steal' ? '#ffd0ac' : action === 'help' ? '#bcecff' : '#fff3a6',
        stroke: '#423322', strokeThickness: 3, align: 'center',
      }).setOrigin(0.5, 1).setDepth(999)
    this.feedback.add(text)
    this.scene.tweens.add({ targets: text, y: text.y - (this.reduceMotion ? 0 : 24), alpha: 0,
      duration: this.reduceMotion ? 1500 : 2500,
      onComplete: () => { this.feedback.delete(text); text.destroy() } })
    if (action === 'help' && !this.reduceMotion) {
      for (let index = 0; index < 5; index++) {
        const grain = this.scene.add.circle(visual.sprite.x - 10, visual.sprite.y - 30, 1.2, 0xf9cc68).setDepth(490)
        this.feedback.add(grain)
        this.scene.tweens.add({ targets: grain, x: visual.sprite.x + index * 2,
          y: visual.sprite.y - 2, alpha: 0, delay: index * 50, duration: 450,
          onComplete: () => { this.feedback.delete(grain); grain.destroy() } })
      }
    }
  }

  private destroyAnimal(visual: Visual) {
    for (const object of [visual.sprite, visual.product, visual.shadow, visual.target]) {
      if (!object) continue
      this.scene.tweens.killTweensOf(object)
      object.destroy()
    }
  }

  private clearObjects() {
    for (const visual of this.animals.values()) this.destroyAnimal(visual)
    this.animals.clear()
    for (const object of [...this.objects, ...this.feedback]) {
      this.scene.tweens.killTweensOf(object)
      object.destroy()
    }
    this.objects = []
    this.feedback.clear()
  }

  destroy() {
    this.scene.events.off(Phaser.Scenes.Events.UPDATE, this.update)
    window.removeEventListener('animal:collect:done', this.onCollect)
    window.removeEventListener('animal:help:done', this.onHelp)
    window.removeEventListener('animal:steal:done', this.onSteal)
    this.clearObjects()
    this.state = null
  }
}
