import { useEffect, useId, useRef, useState, type ChangeEvent, type RefObject } from 'react'
import { createPortal } from 'react-dom'

type PhotoSourcePickerProps = {
  triggerLabel: string
  title: string
  description: string
  disabled: boolean
  cameraAccept: string
  galleryAccept: string
  onFileSelected: (file: File) => void
}

/**
 * Shared mobile-first camera/gallery chooser for invoice photo workflows.
 * The source choices are rendered in a modal bottom sheet, never inline in the form.
 */
export function PhotoSourcePicker({
  triggerLabel,
  title,
  description,
  disabled,
  cameraAccept,
  galleryAccept,
  onFileSelected
}: PhotoSourcePickerProps) {
  const [open, setOpen] = useState(false)
  const titleId = useId()
  const descriptionId = useId()
  const triggerRef = useRef<HTMLButtonElement>(null)
  const cameraInputRef = useRef<HTMLInputElement>(null)
  const galleryInputRef = useRef<HTMLInputElement>(null)
  const layerRef = useRef<HTMLDivElement>(null)
  const cameraActionRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return

    const previousFocus = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null
    cameraActionRef.current?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setOpen(false)
        return
      }

      if (event.key !== 'Tab') return
      const controls = Array.from(
        layerRef.current?.querySelectorAll<HTMLButtonElement>('button:not([disabled])') ?? []
      ).filter(button => button.tabIndex !== -1)
      if (controls.length === 0) return
      const first = controls[0]
      const last = controls[controls.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      if (previousFocus?.isConnected) previousFocus.focus()
      else triggerRef.current?.focus()
    }
  }, [open])

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0]
    event.currentTarget.value = ''
    setOpen(false)
    if (file) onFileSelected(file)
  }

  function chooseSource(input: RefObject<HTMLInputElement | null>) {
    setOpen(false)
    input.current?.click()
  }

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="secondary photo-source-trigger"
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={() => setOpen(true)}
      >
        {triggerLabel}
      </button>

      <input
        ref={cameraInputRef}
        hidden
        type="file"
        accept={cameraAccept}
        capture="environment"
        onChange={handleFileChange}
      />
      <input
        ref={galleryInputRef}
        hidden
        type="file"
        accept={galleryAccept}
        onChange={handleFileChange}
      />

      {open && createPortal(
        <div className="photo-source-layer" ref={layerRef}>
          <button
            type="button"
            className="photo-source-backdrop"
            aria-label="Close photo source options"
            tabIndex={-1}
            onClick={() => setOpen(false)}
          />
          <section
            className="photo-source-sheet"
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            aria-describedby={descriptionId}
          >
            <div className="photo-source-handle" aria-hidden="true" />
            <header className="photo-source-header">
              <div>
                <h3 id={titleId}>{title}</h3>
                <p id={descriptionId}>{description}</p>
              </div>
              <button
                type="button"
                className="photo-source-close"
                aria-label="Close"
                onClick={() => setOpen(false)}
              >
                ×
              </button>
            </header>

            <button
              ref={cameraActionRef}
              type="button"
              className="photo-source-option"
              onClick={() => chooseSource(cameraInputRef)}
            >
              <span className="photo-source-option-icon" aria-hidden="true">📷</span>
              <span className="photo-source-option-copy">
                <strong>Take a photo</strong>
                <small>Open the device camera</small>
              </span>
              <span className="photo-source-chevron" aria-hidden="true">›</span>
            </button>

            <button
              type="button"
              className="photo-source-option"
              onClick={() => chooseSource(galleryInputRef)}
            >
              <span className="photo-source-option-icon" aria-hidden="true">🖼️</span>
              <span className="photo-source-option-copy">
                <strong>Choose from device</strong>
                <small>Select an existing photo or supported file</small>
              </span>
              <span className="photo-source-chevron" aria-hidden="true">›</span>
            </button>

            <button
              type="button"
              className="photo-source-cancel"
              onClick={() => setOpen(false)}
            >
              Cancel
            </button>
          </section>
        </div>,
        document.body
      )}
    </>
  )
}
