import { useEffect, useState } from 'react'
import { Image } from 'lucide-react'
import type { Place } from '../../contracts/places'
import {
  getPlacePhoto,
  type PlacePhotoData,
} from '../journey-planning/googleMaps'
import styles from './JourneyDetails.module.css'

export function PlacePhoto({ place }: { place: Place }) {
  const [photo, setPhoto] = useState<PlacePhotoData | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    void getPlacePhoto(place.placeId, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setPhoto(result)
      })
      .catch(() => {
        /* A neutral slot is sufficient; do not repeat map errors. */
      })
    return () => controller.abort()
  }, [place.placeId])
  return (
    <figure className={styles.photo}>
      {photo && !failed ? (
        <>
          <img
            src={photo.src}
            alt={place.name}
            onError={() => setFailed(true)}
          />
          <figcaption>
            <span translate="no" className={styles.googleAttribution}>
              Google Maps
            </span>
            {photo.authors.map((author, index) =>
              author.url ? (
                <a
                  key={index}
                  href={author.url}
                  target="_blank"
                  rel="noreferrer"
                >
                  {author.name}
                </a>
              ) : (
                <span key={index}>{author.name}</span>
              ),
            )}
          </figcaption>
        </>
      ) : (
        <div
          className={styles.photoFallback}
          role="img"
          aria-label={`Photo unavailable for ${place.name}`}
        >
          <Image aria-hidden="true" />
          <span>Photo unavailable</span>
        </div>
      )}
    </figure>
  )
}
