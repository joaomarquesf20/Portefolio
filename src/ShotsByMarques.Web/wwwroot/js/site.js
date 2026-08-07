// ---------- Photo selection cart (localStorage) ----------
const CART_KEY = "sbm_cart"; // { [galleryId]: [photoId, ...] }

function getCart() {
  try { return JSON.parse(localStorage.getItem(CART_KEY)) || {}; }
  catch { return {}; }
}

function saveCart(cart) {
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
  window.dispatchEvent(new CustomEvent("sbm:cart-change", { detail: countSelected() }));
}

function selectedPhotoIds() {
  const cart = getCart();
  return Object.values(cart).flat();
}

function isSelected(galleryId, photoId) {
  return (getCart()[galleryId] || []).includes(photoId);
}

function togglePhoto(galleryId, photoId) {
  const cart = getCart();
  const list = cart[galleryId] || [];
  const idx = list.indexOf(photoId);
  if (idx >= 0) list.splice(idx, 1); else list.push(photoId);
  if (list.length === 0) delete cart[galleryId]; else cart[galleryId] = list;
  saveCart(cart);
}

function clearCart() {
  localStorage.removeItem(CART_KEY);
  window.dispatchEvent(new CustomEvent("sbm:cart-change", { detail: 0 }));
}

function countSelected() {
  return Object.values(getCart()).reduce((sum, list) => sum + list.length, 0);
}

function refreshCartBadge() {
  const count = countSelected();
  document.querySelectorAll("[data-cart-badge]").forEach(badge => {
    badge.textContent = count;
    badge.classList.toggle("hidden", count === 0);
  });
  document.querySelectorAll("[data-cart-count]").forEach(el => {
    el.textContent = count;
  });
}

document.addEventListener("DOMContentLoaded", () => {
  refreshCartBadge();
  window.addEventListener("sbm:cart-change", refreshCartBadge);

  // ----- Photo protection: block right-click "Save image as…" and drag -----
  // This is a deterrent, not real protection — the actual protection is that
  // previews are watermarked and downscaled; originals are only served after payment.
  document.addEventListener("contextmenu", (e) => {
    if (e.target.closest(".gallery-item, #lightbox, .checkout-photo")) e.preventDefault();
  });
  document.querySelectorAll(".gallery-item img, #lightbox img").forEach(img => {
    img.draggable = false;
    img.addEventListener("dragstart", (e) => e.preventDefault());
  });

  // ----- Gallery page: selection UI -----
  document.querySelectorAll("[data-gallery-id]").forEach(item => {
    const galleryId = Number(item.dataset.galleryId);
    const photoId = Number(item.dataset.photoId);
    const toggle = item.querySelector("[data-toggle]");
    const update = () => {
      const selected = isSelected(galleryId, photoId);
      item.classList.toggle("selected", selected);
      if (toggle) toggle.textContent = selected ? toggle.dataset.removeText : toggle.dataset.addText;
    };
    if (toggle) {
      toggle.addEventListener("click", (e) => {
        e.stopPropagation();
        togglePhoto(galleryId, photoId);
        update();
        const btn = document.querySelector("[data-checkout-continue]");
        if (btn) btn.classList.toggle("hidden", countSelected() === 0);
      });
    }
    item.addEventListener("click", (e) => {
      if (e.target.closest("[data-toggle]")) return;
      openLightbox(Array.from(document.querySelectorAll("[data-gallery-id]")), item);
    });
    update();
  });

  // ----- Checkout continue button (gallery page) -----
  const continueBtn = document.querySelector("[data-checkout-continue]");
  if (continueBtn) {
    const go = () => {
      const ids = selectedPhotoIds();
      if (ids.length === 0) return;
      window.location.href = "/Checkout?ids=" + ids.join(",");
    };
    continueBtn.addEventListener("click", go);
    continueBtn.classList.toggle("hidden", countSelected() === 0);
  }

  // ----- Navbar cart icon: carry the selected photo ids from localStorage -----
  const cartLink = document.querySelector("a[href=\"/Checkout\"]");
  if (cartLink) {
    cartLink.addEventListener("click", (e) => {
      const ids = selectedPhotoIds();
      if (ids.length === 0) return; // fall through to /Checkout (empty state)
      e.preventDefault();
      window.location.href = "/Checkout?ids=" + ids.join(",");
    });
  }

  // ----- Select all (gallery page) -----
  document.querySelectorAll("[data-select-all]").forEach(btn => {
    btn.addEventListener("click", () => {
      const cart = getCart();
      let added = 0;
      document.querySelectorAll("[data-gallery-id][data-sale='true']").forEach(item => {
        const galleryId = Number(item.dataset.galleryId);
        const photoId = Number(item.dataset.photoId);
        const list = cart[galleryId] || [];
        if (!list.includes(photoId)) { list.push(photoId); added++; }
        cart[galleryId] = list;
        item.classList.add("selected");
        const toggle = item.querySelector("[data-toggle]");
        if (toggle) toggle.textContent = toggle.dataset.removeText;
      });
      if (added > 0) saveCart(cart);
      if (continueBtn) continueBtn.classList.toggle("hidden", countSelected() === 0);
    });
  });

  // ----- Clear selection -----
  document.querySelectorAll("[data-clear-cart]").forEach(btn => {
    btn.addEventListener("click", () => {
      clearCart();
      document.querySelectorAll("[data-gallery-id]").forEach(item => {
        const galleryId = Number(item.dataset.galleryId);
        const photoId = Number(item.dataset.photoId);
        item.classList.remove("selected");
        const toggle = item.querySelector("[data-toggle]");
        if (toggle) toggle.textContent = toggle.dataset.addText;
      });
      if (continueBtn) continueBtn.classList.add("hidden");
    });
  });

  // ----- Download all session photos (each is a POST form with its own token) -----
  const downloadAllBtn = document.querySelector("[data-download-all]");
  if (downloadAllBtn) {
    downloadAllBtn.addEventListener("click", () => {
      const forms = Array.from(document.querySelectorAll("form[data-session-download]"));
      if (forms.length === 0) return;
      downloadAllBtn.disabled = true;
      downloadAllBtn.textContent = "⏳ ...";
      // Submit one at a time with a small delay so the browser allows multiple downloads
      let i = 0;
      const next = () => {
        if (i >= forms.length) {
          downloadAllBtn.disabled = false;
          downloadAllBtn.textContent = "↓ " + (downloadAllBtn.dataset.doneText || "");
          return;
        }
        forms[i].submit();
        i++;
        setTimeout(next, 600);
      };
      next();
    });
  }

  // ----- Lightbox with zoom -----
  const lb = {
    box: null, img: null, title: null, index: 0, items: [],
    scale: 1, tx: 0, ty: 0, dragging: false, moved: false
  };

  window.openLightbox = (items, current) => {
    lb.box = document.getElementById("lightbox");
    lb.img = document.getElementById("lightbox-img");
    lb.title = document.getElementById("lightbox-title");
    lb.items = items;
    lb.index = items.indexOf(current);
    lbResetZoom();
    lbShow();
    lb.box.classList.add("open");
    document.body.style.overflow = "hidden";
  };

  const lbResetZoom = () => { lb.scale = 1; lb.tx = 0; lb.ty = 0; lbApply(); };
  const lbApply = () => {
    if (!lb.img) return;
    lb.img.style.transform = `translate(${lb.tx}px, ${lb.ty}px) scale(${lb.scale})`;
    lb.img.classList.toggle("lb-zoomed", lb.scale > 1);
  };
  const lbZoomAt = (factor, cx, cy) => {
    const next = Math.min(6, Math.max(1, lb.scale * factor));
    if (next === lb.scale) return;
    const rect = lb.img.getBoundingClientRect();
    const px = cx - (rect.left + rect.width / 2);
    const py = cy - (rect.top + rect.height / 2);
    lb.tx += px - px * (next / lb.scale);
    lb.ty += py - py * (next / lb.scale);
    lb.scale = next;
    lbApply();
  };
  const lbPan = (dx, dy) => { lb.tx += dx; lb.ty += dy; lbApply(); };

  const lbShow = () => {
    const el = lb.items[lb.index];
    lb.img.src = el.querySelector("img").src;
    lb.title.textContent = el.dataset.title + (el.dataset.price ? " — " + el.dataset.price : "");
    lbResetZoom();
  };
  window.lbNav = (dir) => {
    lb.index = (lb.index + dir + lb.items.length) % lb.items.length;
    lbShow();
  };

  // Zoom controls (bound once — safe with addEventListener)
  const lbBox = document.getElementById("lightbox");
  if (lbBox) {
    lbBox.addEventListener("click", (e) => {
      // Close when clicking the backdrop (stage or box background), not the image/buttons.
      if (e.target === lbBox || e.target.closest("#lb-stage")) closeLightbox();
    });
    lbBox.querySelector("[data-lb-zoom-in]")?.addEventListener("click", (e) => {
      e.stopPropagation();
      lbZoomAt(1.4, window.innerWidth / 2, window.innerHeight / 2);
    });
    lbBox.querySelector("[data-lb-zoom-out]")?.addEventListener("click", (e) => {
      e.stopPropagation();
      lbZoomAt(1 / 1.4, window.innerWidth / 2, window.innerHeight / 2);
    });
    lbBox.querySelector("[data-lb-zoom-reset]")?.addEventListener("click", (e) => {
      e.stopPropagation();
      lbResetZoom();
    });
    lbBox.addEventListener("wheel", (e) => {
      e.preventDefault();
      // Zoom always centered on the photo (not at the cursor position)
      const rect = lb.img.getBoundingClientRect();
      lbZoomAt(e.deltaY < 0 ? 1.15 : 1 / 1.15, rect.left + rect.width / 2, rect.top + rect.height / 2);
    }, { passive: false });
  }

  // Drag to pan (only when zoomed in) — global listeners bound once
  window.addEventListener("mousedown", (e) => {
    if (e.target.closest && e.target.closest("#lightbox-img")) {
      if (lb.scale <= 1) return;
      lb.dragging = true;
      lb.moved = false;
      lb._sx = e.clientX; lb._sy = e.clientY;
      if (lb.img) lb.img.style.transition = "none";
      e.preventDefault();
    }
  });
  window.addEventListener("mousemove", (e) => {
    if (!lb.dragging) return;
    const dx = e.clientX - lb._sx, dy = e.clientY - lb._sy;
    if (Math.abs(dx) + Math.abs(dy) > 3) lb.moved = true;
    lbPan(dx, dy);
    lb._sx = e.clientX; lb._sy = e.clientY;
  });
  window.addEventListener("mouseup", () => {
    if (lb.dragging && lb.img) lb.img.style.transition = "";
    lb.dragging = false;
  });

  // Click on the image toggles zoom (1x -> 2.5x -> back).
  // If the user just dragged (pan), skip the toggle — mouseup already
  // cleared `dragging`, so `moved` is the reliable signal.
  document.getElementById("lightbox-img")?.addEventListener("click", (e) => {
    e.stopPropagation();
    if (lb.dragging || lb.moved) { lb.moved = false; return; }
    if (lb.scale > 1) lbResetZoom();
    else lbZoomAt(2.5, e.clientX, e.clientY);
  });

  const closeLightbox = () => {
    const box = document.getElementById("lightbox");
    if (box) box.classList.remove("open");
    document.body.style.overflow = "";
  };
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") { closeLightbox(); return; }
    // Only navigate when the lightbox is actually open (lb.items empty otherwise)
    if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
      const box = document.getElementById("lightbox");
      if (!box || !box.classList.contains("open") || lb.items.length === 0) return;
      window.lbNav(e.key === "ArrowRight" ? 1 : -1);
    }
  });
  document.querySelectorAll("[data-close-lightbox]").forEach(b => b.addEventListener("click", closeLightbox));

  // ----- Booking: availability slots -----
  const bookingForm = document.getElementById("booking-form");
  if (bookingForm) {
    const typeId = bookingForm.dataset.sessionTypeId;
    const dateInput = document.getElementById("Date");
    const slotsWrap = document.getElementById("slots");
    const timeInput = document.getElementById("TimeSlot");
    const noSlots = document.getElementById("no-slots");

    const loadSlots = async () => {
      if (!dateInput.value) return;
      slotsWrap.innerHTML = "";
      noSlots?.classList.add("hidden");
      timeInput.value = "";
      const res = await fetch(`/Bookings/Available?sessionTypeId=${typeId}&date=${dateInput.value}`);
      const slots = await res.json();
      if (slots.length === 0) { noSlots?.classList.remove("hidden"); return; }
      slots.forEach(slot => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "time-slot";
        btn.textContent = slot;
        btn.addEventListener("click", () => {
          slotsWrap.querySelectorAll(".time-slot").forEach(b => b.classList.remove("active"));
          btn.classList.add("active");
          timeInput.value = slot;
        });
        slotsWrap.appendChild(btn);
      });
    };
    dateInput.addEventListener("change", loadSlots);
    if (dateInput.value) loadSlots();
  }

  // ----- Fade-up on scroll -----
  const observer = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add("visible");
        observer.unobserve(entry.target);
      }
    });
  }, { threshold: 0.08 });
  document.querySelectorAll(".fade-up").forEach(el => observer.observe(el));

  // ----- Mobile menu -----
  const menuBtn = document.getElementById("menu-btn");
  const mobileMenu = document.getElementById("mobile-menu");
  menuBtn?.addEventListener("click", () => mobileMenu?.classList.toggle("hidden"));
});
