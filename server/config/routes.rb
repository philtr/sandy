Rails.application.routes.draw do
  root "dashboard#show"

  resource :setup, only: [ :new, :create ]
  resource :session, only: [ :new, :create, :destroy ]
  resource :parent_profile, only: :update
  resource :enrollment_code, only: :update
  resources :devices, only: [ :show, :destroy ] do
    resources :time_grants, only: :create
    resource :screen_time_revocation, only: :create
  end

  namespace :api do
    namespace :v1 do
      resources :enrollments, only: :create
      resource :state, only: :show
      resources :heartbeats, only: :create
      resources :events, only: :create
    end
  end

  get "up" => "rails/health#show", as: :rails_health_check
  get "manifest" => "rails/pwa#manifest", as: :pwa_manifest
  get "service-worker" => "rails/pwa#service_worker", as: :pwa_service_worker
end
