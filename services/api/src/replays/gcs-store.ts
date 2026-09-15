import { Storage } from '@google-cloud/storage'
import type { ReplayStore } from './store.ts'

/**
 * The player-visible replay collection - solo_execution 9.5 / design 5.1.
 * The CI corpus is a separate collection with its own retention and stays a
 * committed repo seed this phase (design 5.1); this store is not it.
 *
 * The bucket's 30-day lifecycle rule is Task 11's job (infra/terraform);
 * this class only ever writes - it never lists and never deletes. The doc
 * said "writes and lists" until the list() it referred to was removed (see
 * the note at the bottom of this class for why); a class doc that still
 * advertises a method the class does not have is the kind of stale line a
 * reader trusts instead of reading the code.
 */
export class GcsReplayStore implements ReplayStore {
  private readonly storage = new Storage()

  // Explicit field, NOT a constructor parameter property - see
  // config/gcs-store.ts's GcsBundleStore for why.
  private readonly bucketName: string

  constructor(bucketName: string) {
    this.bucketName = bucketName
  }

  private get bucket() { return this.storage.bucket(this.bucketName) }
  private key(serverId: number, playerId: string, issuanceId: string): string {
    return `replays/${serverId}/${playerId}/${issuanceId}.bin`
  }

  async put(serverId: number, playerId: string, issuanceId: string, bytes: string): Promise<void> {
    await this.bucket.file(this.key(serverId, playerId, issuanceId)).save(bytes, {
      contentType: 'application/octet-stream',
      // No resumable session for an object this small - a few hundred
      // bytes, design 5.2 - it costs an extra round trip for nothing.
      resumable: false,
    })
  }

  // No list() here - review finding. It is not on the ReplayStore
  // interface (see store.ts's LocalReplayStore for why: the one caller is
  // a test, which holds a concrete LocalReplayStore already), and nothing
  // in production needs to enumerate this bucket, so this class carries no
  // unpaginated bucket.getFiles() surface it has no caller for.
}
